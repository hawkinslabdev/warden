using Warden.Endpoints;

namespace Warden.Tests;

public sealed class FormatDurationTests
{
    [Theory]
    [InlineData(23 * 3600 + 50 * 60, "24h")]
    [InlineData(23 * 3600, "23h")]
    [InlineData(24 * 60 + 36, "25 min")]
    [InlineData(29 * 60, "29 min")]
    [InlineData(0, "1 min")]
    public void FormatDuration_RoundsToNearestUnit(double totalSeconds, string expected) =>
        Assert.Equal(expected, StatusEndpoints.FormatDuration(TimeSpan.FromSeconds(totalSeconds)));

    [Fact]
    public void FormatDuration_RoundsDaysToNearestDay() =>
        Assert.Equal("2d", StatusEndpoints.FormatDuration(TimeSpan.FromHours(40)));
}
