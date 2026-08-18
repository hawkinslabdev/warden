using System.Text.Json.Nodes;
using Warden.Services;

namespace Warden.Tests;

public sealed class MonitorSchedulerTests
{
    [Theory]
    [InlineData("""{"status":"ok"}""", "$.status", "ok")]
    [InlineData("""{"data":{"db":"up"}}""", "$.data.db", "up")]
    [InlineData("""{"pools":[{"name":"main","ok":true},{"name":"replica","ok":true}]}""", "$.pools[?@.ok==false]", null)]
    [InlineData("""{"pools":["down","up","up"]}""", "$.pools[0]", "down")]
    public void ExtractJsonValue_SupportsFullJsonPathSyntax(string json, string path, string? expected)
    {
        var node = JsonNode.Parse(json);
        Assert.Equal(expected, MonitorScheduler.ExtractJsonValue(node, path));
    }

    [Fact]
    public void ExtractJsonValue_MissingSegmentReturnsNull()
    {
        var node = JsonNode.Parse("""{"status":"ok"}""");
        Assert.Null(MonitorScheduler.ExtractJsonValue(node, "$.missing.path"));
    }

    [Fact]
    public void EvaluateCertExpiry_UpWhenWellBeforeWarningWindow()
    {
        var now = new DateTime(2026, 1, 1);
        var (up, error) = MonitorScheduler.EvaluateCertExpiry(now.AddDays(60), warnDaysBefore: 30, now);
        Assert.True(up);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateCertExpiry_DownWithinWarningWindow()
    {
        var now = new DateTime(2026, 1, 1);
        var (up, error) = MonitorScheduler.EvaluateCertExpiry(now.AddDays(10), warnDaysBefore: 30, now);
        Assert.False(up);
        Assert.Contains("expires in", error);
    }

    [Fact]
    public void EvaluateCertExpiry_DownWhenAlreadyExpired()
    {
        var now = new DateTime(2026, 1, 1);
        var (up, error) = MonitorScheduler.EvaluateCertExpiry(now.AddDays(-1), warnDaysBefore: 30, now);
        Assert.False(up);
        Assert.Contains("expired", error);
    }
}
