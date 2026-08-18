using System.Text.Json;
using Warden.Services;

namespace Warden.Tests;

public sealed class MonitorSchedulerTests
{
    [Theory]
    [InlineData("""{"status":"ok"}""", "$.status", "ok")]
    [InlineData("""{"data":{"db":"up"}}""", "$.data.db", "up")]
    [InlineData("""{"data":{"db":"up"}}""", "data.db", "up")]
    public void ExtractJsonValue_ReadsNestedDotPath(string json, string path, string expected)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(expected, MonitorScheduler.ExtractJsonValue(doc.RootElement, path));
    }

    [Fact]
    public void ExtractJsonValue_MissingSegmentReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"status":"ok"}""");
        Assert.Null(MonitorScheduler.ExtractJsonValue(doc.RootElement, "$.missing.path"));
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
