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

    [Theory]
    [InlineData(1, 0, true)]   // no Retries configured: fires on the very first failure
    [InlineData(2, 0, false)]  // still down on a later check: no repeat fire
    [InlineData(3, 2, true)]   // Retries: 2 - fires on the 3rd consecutive failure
    [InlineData(2, 2, false)]  // within the retry window: pending, no fire yet
    [InlineData(4, 2, false)]  // still down past the transition: no repeat fire
    public void ShouldFireDownWebhook_FiresOnlyOnTheTransition(int consecutiveFailures, int retryThreshold, bool expected)
    {
        Assert.Equal(expected, MonitorScheduler.ShouldFireDownWebhook(consecutiveFailures, retryThreshold));
    }

    // proof-of-fix: many webhook URLs (Slack/Discord/Teams) carry their auth secret in the path or query.
    // logging the raw URL on failure would leak it (OWASP A09/A02); SafeHost must strip everything but scheme+host+port.
    [Theory]
    [InlineData("https://hooks.slack.com/services/T00/B00/xoxb-super-secret-token", "https://hooks.slack.com")]
    [InlineData("https://example.com:8443/hook?token=super-secret-token", "https://example.com:8443")]
    [InlineData("https://user:pass@example.com/hook", "https://example.com")]
    public void SafeHost_NeverLeaksPathQueryOrCredentials(string webhookUrl, string expectedLoggedValue)
    {
        var safe = MonitorScheduler.SafeHost(webhookUrl);
        Assert.Equal(expectedLoggedValue, safe);
        Assert.DoesNotContain("secret", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pass", safe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeHost_InvalidUrl_DoesNotThrowOrEcho()
    {
        Assert.Equal("(invalid url)", MonitorScheduler.SafeHost("not a url"));
    }
}
