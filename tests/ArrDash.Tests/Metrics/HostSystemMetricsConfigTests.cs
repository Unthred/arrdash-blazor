using ArrDash.Models;
using ArrDash.Services;

namespace ArrDash.Tests.Metrics;

public sealed class HostSystemMetricsConfigTests
{
    [Fact]
    public void ResolveLabel_prefers_settings_over_env()
    {
        var original = Environment.GetEnvironmentVariable("ARRDASH_HOST_LABEL");
        try
        {
            Environment.SetEnvironmentVariable("ARRDASH_HOST_LABEL", "FromEnv");
            var label = HostSystemMetricsService.ResolveLabel(new UserLayoutPreferences { MetricsHostLabel = "FromSettings" });
            Assert.Equal("FromSettings", label);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ARRDASH_HOST_LABEL", original);
        }
    }

    [Fact]
    public void ResolveLabel_defaults_to_Host()
    {
        var original = Environment.GetEnvironmentVariable("ARRDASH_HOST_LABEL");
        try
        {
            Environment.SetEnvironmentVariable("ARRDASH_HOST_LABEL", null);
            var label = HostSystemMetricsService.ResolveLabel(new UserLayoutPreferences());
            Assert.Equal("Host", label);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ARRDASH_HOST_LABEL", original);
        }
    }

    [Fact]
    public void ResolveDiskPaths_supports_comma_separated_paths()
    {
        var paths = HostSystemMetricsService.ResolveDiskPaths(new UserLayoutPreferences
        {
            MetricsDiskPath = "/data, /backup"
        });

        Assert.Equal(["/data", "/backup"], paths);
    }

    [Fact]
    public void ResolveDiskPaths_prefers_settings_over_env()
    {
        var original = Environment.GetEnvironmentVariable("ARRDASH_DISK_PATH");
        try
        {
            Environment.SetEnvironmentVariable("ARRDASH_DISK_PATH", "/from-env");
            var paths = HostSystemMetricsService.ResolveDiskPaths(new UserLayoutPreferences { MetricsDiskPath = "/from-settings" });
            Assert.Equal(["/from-settings"], paths);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ARRDASH_DISK_PATH", original);
        }
    }
}
