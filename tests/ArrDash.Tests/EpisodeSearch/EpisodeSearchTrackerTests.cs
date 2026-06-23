using ArrDash.Services;
using ArrDash.Services.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;

namespace ArrDash.Tests.EpisodeSearch;

public sealed class FakeSonarrEpisodeSearchMonitor : ISonarrEpisodeSearchMonitor
{
    public bool IsConfigured { get; set; } = true;
    public string? CommandStatus { get; set; } = "started";
    public bool HasFile { get; set; }
    public bool InQueue { get; set; }
    public bool HasGrab { get; set; }

    public Task<string?> GetCommandStatusAsync(int commandId, CancellationToken ct) =>
        Task.FromResult(CommandStatus);

    public Task<bool> GetEpisodeHasFileAsync(int seriesId, int seasonNumber, int episodeNumber, CancellationToken ct) =>
        Task.FromResult(HasFile);

    public Task<bool> IsEpisodeInQueueAsync(int episodeId, CancellationToken ct) =>
        Task.FromResult(InQueue);

    public Task<bool> HasRecentGrabAsync(int episodeId, DateTimeOffset since, CancellationToken ct) =>
        Task.FromResult(HasGrab);
}

public sealed class EpisodeSearchTrackerTests
{
    private static EpisodeSearchRequest CreateRequest(int episodeId = 42, int? commandId = 7) =>
        new(1, 1, 4, episodeId, commandId, "Nemesis S01E04");

    [Fact]
    public void Track_deduplicates_same_episode()
    {
        var tracker = CreateTracker(out _);
        var notifications = CaptureNotifications(tracker);

        tracker.Track(CreateRequest());

        Assert.True(tracker.IsPending(1, 1, 4));
        var count = notifications.Count;

        tracker.Track(CreateRequest());

        Assert.True(tracker.IsPending(1, 1, 4));
        Assert.Equal(count, notifications.Count);
    }

    [Fact]
    public async Task PollOnceAsync_transitions_to_downloading_when_grabbed()
    {
        var tracker = CreateTracker(out var monitor);
        var notifications = CaptureNotifications(tracker);
        monitor.HasGrab = true;

        tracker.Track(CreateRequest());
        await tracker.PollOnceAsync(CancellationToken.None);

        var downloading = notifications.LastOrDefault(n => n.Phase == EpisodeSearchPhase.Downloading);
        Assert.NotNull(downloading);
        Assert.Contains("downloading", downloading!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Severity.Info, downloading.Severity);
    }

    [Fact]
    public async Task PollOnceAsync_transitions_to_imported_and_refreshes_dashboard()
    {
        var monitor = new FakeSonarrEpisodeSearchMonitor { HasFile = true };
        var refreshCount = 0;
        var tracker = CreateTracker(monitor, () => refreshCount++);
        var notifications = CaptureNotifications(tracker);

        tracker.Track(CreateRequest());
        await tracker.PollOnceAsync(CancellationToken.None);

        var imported = notifications.LastOrDefault(n => n.Phase == EpisodeSearchPhase.Imported);
        Assert.NotNull(imported);
        Assert.Equal(1, refreshCount);
        Assert.False(tracker.IsPending(42));
    }

    [Fact]
    public async Task PollOnceAsync_transitions_to_not_found_after_command_completes_without_grab()
    {
        var now = DateTimeOffset.UtcNow;
        var monitor = new FakeSonarrEpisodeSearchMonitor { CommandStatus = "completed" };
        var tracker = CreateTracker(monitor, utcNow: () => now);
        var notifications = CaptureNotifications(tracker);

        tracker.Track(CreateRequest());
        await tracker.PollOnceAsync(CancellationToken.None);

        now += EpisodeSearchTrackerService.NotFoundGrace + TimeSpan.FromSeconds(1);
        await tracker.PollOnceAsync(CancellationToken.None);

        Assert.Contains(notifications, n => n.Phase == EpisodeSearchPhase.NotFound);
        Assert.False(tracker.IsPending(42));
    }

    [Fact]
    public async Task PollOnceAsync_transitions_to_timed_out_after_max_watch_while_downloading()
    {
        var now = DateTimeOffset.UtcNow;
        var monitor = new FakeSonarrEpisodeSearchMonitor { HasGrab = true };
        var tracker = CreateTracker(monitor, utcNow: () => now);
        var notifications = CaptureNotifications(tracker);

        tracker.Track(CreateRequest());
        await tracker.PollOnceAsync(CancellationToken.None);

        now += EpisodeSearchTrackerService.MaxWatch + TimeSpan.FromSeconds(1);
        await tracker.PollOnceAsync(CancellationToken.None);

        Assert.Contains(notifications, n => n.Phase == EpisodeSearchPhase.TimedOut);
        Assert.False(tracker.IsPending(42));
    }

    [Fact]
    public async Task GetStatus_keeps_terminal_result_visible_for_follow_up()
    {
        var now = DateTimeOffset.UtcNow;
        var monitor = new FakeSonarrEpisodeSearchMonitor { CommandStatus = "completed" };
        var tracker = CreateTracker(monitor, utcNow: () => now);

        tracker.Track(CreateRequest());
        await tracker.PollOnceAsync(CancellationToken.None);

        now += EpisodeSearchTrackerService.NotFoundGrace + TimeSpan.FromSeconds(1);
        await tracker.PollOnceAsync(CancellationToken.None);

        var status = tracker.GetStatus(1, 1, 4);
        Assert.NotNull(status);
        Assert.Equal(EpisodeSearchPhase.NotFound, status!.Phase);
        Assert.False(status.IsActive);

        now += EpisodeSearchTrackerService.ResultDisplayDuration + TimeSpan.FromSeconds(1);
        Assert.Null(tracker.GetStatus(1, 1, 4));
    }

    private static EpisodeSearchTrackerService CreateTracker(
        out FakeSonarrEpisodeSearchMonitor monitor,
        Action? onRefresh = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        monitor = new FakeSonarrEpisodeSearchMonitor();
        return CreateTracker(monitor, onRefresh, utcNow);
    }

    private static EpisodeSearchTrackerService CreateTracker(
        FakeSonarrEpisodeSearchMonitor monitor,
        Action? onRefresh = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        var refresh = new FakeDashboardRefresher(onRefresh);
        return new EpisodeSearchTrackerService(
            monitor,
            refresh,
            NullLogger<EpisodeSearchTrackerService>.Instance,
            utcNow);
    }

    private static List<EpisodeSearchNotification> CaptureNotifications(EpisodeSearchTrackerService tracker)
    {
        var notifications = new List<EpisodeSearchNotification>();
        tracker.StatusChanged += notifications.Add;
        return notifications;
    }

    private sealed class FakeDashboardRefresher(Action? onRefresh) : IDashboardRefresher
    {
        public Task RefreshAsync(CancellationToken ct)
        {
            onRefresh?.Invoke();
            return Task.CompletedTask;
        }
    }
}
