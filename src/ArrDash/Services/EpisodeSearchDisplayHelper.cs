using ArrDash.Models;

namespace ArrDash.Services;

public static class EpisodeSearchDisplayHelper
{
    public static string GetPhaseClass(EpisodeSearchPhase? phase) => phase switch
    {
        EpisodeSearchPhase.Queued or EpisodeSearchPhase.Searching => "episode-badge-phase-searching",
        EpisodeSearchPhase.Downloading => "episode-badge-phase-downloading",
        EpisodeSearchPhase.Imported => "episode-badge-phase-imported",
        EpisodeSearchPhase.NotFound => "episode-badge-phase-notfound",
        EpisodeSearchPhase.TimedOut => "episode-badge-phase-timedout",
        _ => ""
    };

    public static string GetStatusIcon(EpisodeSearchPhase phase) => phase switch
    {
        EpisodeSearchPhase.Queued or EpisodeSearchPhase.Searching => "search",
        EpisodeSearchPhase.Downloading => "download",
        EpisodeSearchPhase.Imported => "check",
        EpisodeSearchPhase.NotFound => "search_off",
        EpisodeSearchPhase.TimedOut => "schedule",
        _ => "search"
    };

    public static IReadOnlyList<string> GetTooltipLines(
        EpisodeSearchPhase? phase,
        string label,
        DateTimeOffset? updatedAt,
        MissingEpisodeClickAction clickAction)
    {
        if (phase is null)
        {
            return clickAction == MissingEpisodeClickAction.OpenSonarrAndSearch
                ? ["Missing on disk", $"Click to open Sonarr and search for {label}"]
                : ["Missing on disk", $"Click to search Sonarr for {label}"];
        }

        var when = FormatUpdatedAt(updatedAt);
        var statusLine = string.IsNullOrWhiteSpace(when) ? label : $"{label} · {when}";

        return phase switch
        {
            EpisodeSearchPhase.Queued =>
            [
                "Sonarr search queued",
                statusLine,
                "Waiting for Sonarr to start indexer lookup"
            ],
            EpisodeSearchPhase.Searching =>
            [
                "Searching Sonarr indexers",
                statusLine,
                "ArrDash checks every 15 seconds"
            ],
            EpisodeSearchPhase.Downloading =>
            [
                "Release grabbed",
                statusLine,
                "Downloading via your client",
                "ArrDash will notify when imported"
            ],
            EpisodeSearchPhase.Imported =>
            [
                "Imported to your library",
                statusLine
            ],
            EpisodeSearchPhase.NotFound =>
            [
                "No release found",
                statusLine,
                "Sonarr finished searching",
                "Nothing matched your indexers"
            ],
            EpisodeSearchPhase.TimedOut =>
            [
                "Still not on disk after 30 minutes",
                statusLine,
                "Check Sonarr activity or try again"
            ],
            _ => [label]
        };
    }

    public static string GetTooltipSummary(IReadOnlyList<string> lines) =>
        lines.Count > 0 ? string.Join(" — ", lines) : "";

    private static string FormatUpdatedAt(DateTimeOffset? updatedAt)
    {
        if (updatedAt is null)
            return "";

        var age = DateTimeOffset.UtcNow - updatedAt.Value;
        if (age < TimeSpan.FromMinutes(1))
            return "just now";
        if (age < TimeSpan.FromHours(1))
            return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromDays(1))
            return $"{(int)age.TotalHours} hr ago";
        return updatedAt.Value.ToLocalTime().ToString("ddd HH:mm");
    }
}
