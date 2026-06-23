using ArrDash.Models;
using ArrDash.Services;

namespace ArrDash.Tests.EpisodeSearch;

public class EpisodeSearchDisplayHelperTests
{
    [Theory]
    [InlineData(EpisodeSearchPhase.Searching, "episode-badge-phase-searching")]
    [InlineData(EpisodeSearchPhase.Downloading, "episode-badge-phase-downloading")]
    [InlineData(EpisodeSearchPhase.NotFound, "episode-badge-phase-notfound")]
    public void GetPhaseClass_maps_active_phases(EpisodeSearchPhase phase, string expectedClass)
    {
        Assert.Equal(expectedClass, EpisodeSearchDisplayHelper.GetPhaseClass(phase));
    }

    [Fact]
    public void GetTooltipLines_includes_missing_click_guidance_when_idle()
    {
        var lines = EpisodeSearchDisplayHelper.GetTooltipLines(
            null,
            "Nemesis S01E04",
            null,
            MissingEpisodeClickAction.SearchOnly);

        Assert.Equal(2, lines.Count);
        Assert.Equal("Missing on disk", lines[0]);
        Assert.Contains("Nemesis S01E04", lines[1]);
    }

    [Fact]
    public void GetTooltipLines_splits_searching_status_into_short_lines()
    {
        var lines = EpisodeSearchDisplayHelper.GetTooltipLines(
            EpisodeSearchPhase.Searching,
            "Nemesis S01E04",
            DateTimeOffset.UtcNow.AddMinutes(-2),
            MissingEpisodeClickAction.SearchOnly);

        Assert.True(lines.Count >= 3);
        Assert.Equal("Searching Sonarr indexers", lines[0]);
        Assert.Contains("Nemesis S01E04", lines[1]);
        Assert.Contains("2 min ago", lines[1]);
        Assert.Equal("ArrDash checks every 15 seconds", lines[^1]);
    }
}
