using Warden.Services;

namespace Warden.Tests;

public sealed class GitContentSyncServiceTests
{
    [Theory]
    [InlineData("*/5 * * * *", "2026-08-21T10:02:00", "2026-08-21T10:05:00")]
    [InlineData("*/5 * * * *", "2026-08-21T10:05:00", "2026-08-21T10:10:00")]
    [InlineData("0 2 * * *", "2026-08-21T10:02:00", "2026-08-22T02:00:00")]
    [InlineData("0 */6 * * *", "2026-08-21T10:02:00", "2026-08-21T12:00:00")]
    [InlineData("30 9 * * 1", "2026-08-21T10:02:00", "2026-08-24T09:30:00")]
    public void NextOccurrence_MatchesExpectedSchedule(string cron, string after, string expected)
    {
        Assert.True(CronSchedule.TryParse(cron, out var schedule));
        Assert.Equal(DateTime.Parse(expected), schedule.NextOccurrence(DateTime.Parse(after)));
    }

    [Theory]
    [InlineData("* * * *")]
    [InlineData("60 * * * *")]
    [InlineData("* * * 13 *")]
    public void TryParse_RejectsInvalidExpressions(string cron) => Assert.False(CronSchedule.TryParse(cron, out _));
}
