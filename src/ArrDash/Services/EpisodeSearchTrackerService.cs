using System.Collections.Concurrent;
using ArrDash.Services.Clients;
using MudBlazor;

namespace ArrDash.Services;

public enum EpisodeSearchPhase
{
    Queued,
    Searching,
    Downloading,
    Imported,
    NotFound,
    TimedOut
}

public sealed record EpisodeSearchRequest(
    int SeriesId,
    int SeasonNumber,
    int EpisodeNumber,
    int EpisodeId,
    int? CommandId,
    string Label);

public sealed record EpisodeSearchNotification(
    int EpisodeId,
    string Label,
    EpisodeSearchPhase Phase,
    string Message,
    Severity Severity);

public sealed record EpisodeSearchStatus(
    EpisodeSearchPhase Phase,
    string Message,
    DateTimeOffset UpdatedAt,
    bool IsActive);

public sealed class EpisodeSearchTrackerService : IDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan NotFoundGrace = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MaxWatch = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ResultDisplayDuration = TimeSpan.FromMinutes(15);

    private readonly ISonarrEpisodeSearchMonitor _sonarr;
    private readonly IDashboardRefresher _refresh;
    private readonly ILogger<EpisodeSearchTrackerService> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<int, TrackedSearch> _pending = new();
    private readonly ConcurrentDictionary<int, TrackedSearch> _recentResults = new();
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private Timer? _pollTimer;
    private int _pollRunning;

    public event Action<EpisodeSearchNotification>? StatusChanged;

    public EpisodeSearchTrackerService(
        ISonarrEpisodeSearchMonitor sonarr,
        IDashboardRefresher refresh,
        ILogger<EpisodeSearchTrackerService> logger,
        Func<DateTimeOffset>? utcNow = null)
    {
        _sonarr = sonarr;
        _refresh = refresh;
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsPending(int episodeId) =>
        _pending.TryGetValue(episodeId, out var tracked) && !IsTerminal(tracked.Phase);

    public bool IsPending(int seriesId, int seasonNumber, int episodeNumber) =>
        TryGetActive(seriesId, seasonNumber, episodeNumber, out _);

    public EpisodeSearchStatus? GetStatus(int seriesId, int seasonNumber, int episodeNumber)
    {
        PurgeExpiredResults();

        if (TryGetActive(seriesId, seasonNumber, episodeNumber, out var active))
        {
            return new EpisodeSearchStatus(
                active.Phase,
                active.LastMessage ?? active.Request.Label,
                active.PhaseUpdatedAt,
                IsActive: true);
        }

        if (TryGetRecent(seriesId, seasonNumber, episodeNumber, out var recent))
        {
            return new EpisodeSearchStatus(
                recent.Phase,
                recent.LastMessage ?? recent.Request.Label,
                recent.PhaseUpdatedAt,
                IsActive: false);
        }

        return null;
    }

    public void Track(EpisodeSearchRequest request)
    {
        _recentResults.TryRemove(request.EpisodeId, out _);

        if (_pending.ContainsKey(request.EpisodeId))
            return;

        var tracked = new TrackedSearch
        {
            Request = request,
            Phase = EpisodeSearchPhase.Queued,
            StartedAt = _utcNow(),
            PhaseUpdatedAt = _utcNow(),
            LastMessage = $"Search queued for {request.Label}"
        };

        if (!_pending.TryAdd(request.EpisodeId, tracked))
            return;

        EnsurePollTimer();
    }

    internal async Task PollOnceAsync(CancellationToken ct)
    {
        if (_pending.IsEmpty)
            return;

        if (!await _pollGate.WaitAsync(0, ct))
            return;

        try
        {
            foreach (var (episodeId, tracked) in _pending.ToArray())
            {
                if (IsTerminal(tracked.Phase))
                {
                    RememberResult(tracked);
                    _pending.TryRemove(episodeId, out _);
                    continue;
                }

                try
                {
                    await EvaluateAsync(tracked, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Episode search poll failed for episode {EpisodeId}", episodeId);
                }

                if (IsTerminal(tracked.Phase))
                {
                    RememberResult(tracked);
                    _pending.TryRemove(episodeId, out _);
                }
            }
        }
        finally
        {
            _pollGate.Release();
            if (_pending.IsEmpty)
                StopPollTimer();
        }
    }

    private async Task EvaluateAsync(TrackedSearch tracked, CancellationToken ct)
    {
        var request = tracked.Request;
        var now = _utcNow();

        if (await _sonarr.GetEpisodeHasFileAsync(request.SeriesId, request.SeasonNumber, request.EpisodeNumber, ct))
        {
            Transition(tracked, EpisodeSearchPhase.Imported, $"{request.Label} imported", Severity.Success);
            await _refresh.RefreshAsync(ct);
            return;
        }

        var hasGrab = await _sonarr.HasRecentGrabAsync(request.EpisodeId, tracked.StartedAt, ct);
        var inQueue = await _sonarr.IsEpisodeInQueueAsync(request.EpisodeId, ct);
        if (hasGrab || inQueue)
        {
            if (tracked.Phase != EpisodeSearchPhase.Downloading)
                Transition(tracked, EpisodeSearchPhase.Downloading, $"{request.Label} — release grabbed, downloading…", Severity.Info);
        }

        if (tracked.Phase == EpisodeSearchPhase.Downloading && now - tracked.StartedAt >= MaxWatch)
        {
            Transition(tracked, EpisodeSearchPhase.TimedOut, $"{request.Label} — still not on disk after 30 min", Severity.Warning);
            return;
        }

        if (request.CommandId is int commandId)
        {
            var status = await _sonarr.GetCommandStatusAsync(commandId, ct);
            if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                if (tracked.Phase is EpisodeSearchPhase.Queued)
                    Transition(tracked, EpisodeSearchPhase.Searching, $"Searching Sonarr indexers for {request.Label}", Severity.Info);
                return;
            }

            tracked.CommandCompletedAt ??= now;

            if (tracked.Phase != EpisodeSearchPhase.Downloading &&
                now - tracked.CommandCompletedAt >= NotFoundGrace &&
                !hasGrab &&
                !inQueue)
            {
                Transition(tracked, EpisodeSearchPhase.NotFound, $"{request.Label} — no release found", Severity.Warning);
                return;
            }
        }

        if (now - tracked.StartedAt >= MaxWatch)
            Transition(tracked, EpisodeSearchPhase.TimedOut, $"{request.Label} — still not on disk after 30 min", Severity.Warning);
    }

    private void Transition(TrackedSearch tracked, EpisodeSearchPhase phase, string message, Severity severity)
    {
        if (tracked.Phase == phase && tracked.LastMessage == message)
            return;

        tracked.Phase = phase;
        tracked.PhaseUpdatedAt = _utcNow();
        tracked.LastMessage = message;
        StatusChanged?.Invoke(new EpisodeSearchNotification(
            tracked.Request.EpisodeId,
            tracked.Request.Label,
            phase,
            message,
            severity));
    }

    private void RememberResult(TrackedSearch tracked)
    {
        _recentResults[tracked.Request.EpisodeId] = tracked;
    }

    private void PurgeExpiredResults()
    {
        var now = _utcNow();
        foreach (var (episodeId, tracked) in _recentResults.ToArray())
        {
            if (now - tracked.PhaseUpdatedAt > ResultDisplayDuration)
                _recentResults.TryRemove(episodeId, out _);
        }
    }

    private bool TryGetActive(int seriesId, int seasonNumber, int episodeNumber, out TrackedSearch tracked)
    {
        tracked = _pending.Values.FirstOrDefault(entry =>
            !IsTerminal(entry.Phase) &&
            entry.Request.SeriesId == seriesId &&
            entry.Request.SeasonNumber == seasonNumber &&
            entry.Request.EpisodeNumber == episodeNumber)!;

        return tracked is not null;
    }

    private bool TryGetRecent(int seriesId, int seasonNumber, int episodeNumber, out TrackedSearch tracked)
    {
        tracked = _recentResults.Values.FirstOrDefault(entry =>
            entry.Request.SeriesId == seriesId &&
            entry.Request.SeasonNumber == seasonNumber &&
            entry.Request.EpisodeNumber == episodeNumber)!;

        return tracked is not null;
    }

    private void EnsurePollTimer()
    {
        if (_pollTimer is not null)
            return;

        _pollTimer = new Timer(
            _ => _ = RunPollAsync(),
            null,
            TimeSpan.Zero,
            PollInterval);
    }

    private void StopPollTimer()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private async Task RunPollAsync()
    {
        if (Interlocked.Exchange(ref _pollRunning, 1) == 1)
            return;

        try
        {
            await PollOnceAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Episode search poll loop failed");
        }
        finally
        {
            Interlocked.Exchange(ref _pollRunning, 0);
        }
    }

    private static bool IsTerminal(EpisodeSearchPhase phase) =>
        phase is EpisodeSearchPhase.Imported or EpisodeSearchPhase.NotFound or EpisodeSearchPhase.TimedOut;

    public void Dispose() => StopPollTimer();

    private sealed class TrackedSearch
    {
        public required EpisodeSearchRequest Request { get; init; }
        public EpisodeSearchPhase Phase { get; set; } = EpisodeSearchPhase.Queued;
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset PhaseUpdatedAt { get; set; }
        public DateTimeOffset? CommandCompletedAt { get; set; }
        public string? LastMessage { get; set; }
    }
}
