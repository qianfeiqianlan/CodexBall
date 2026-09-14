using System.Text.Json;
using CodexBall.Core.Codex;
using CodexBall.Core.Utils;

namespace CodexBall.Tests;

public sealed class RateLimitParserTests
{
    [Fact]
    public void Parse_UsesCodexBucketAndSortsByWindowDuration()
    {
        using var document = JsonDocument.Parse("""
        {
          "result": {
            "rateLimitsByLimitId": {
              "codex": {
                "secondary": { "usedPercent": 57, "windowDurationMins": 10080, "resetsAt": 1789790000 },
                "primary": { "usedPercent": 28, "windowDurationMins": 300, "resetsAt": 1789372800 }
              }
            }
          }
        }
        """);

        var (shortWindow, longWindow) = RateLimitParser.Parse(document.RootElement);

        Assert.NotNull(shortWindow);
        Assert.NotNull(longWindow);
        Assert.Equal(72, shortWindow.RemainingPercent);
        Assert.Equal(43, longWindow.RemainingPercent);
        Assert.Equal(300, shortWindow.WindowDurationMins);
        Assert.Equal(10080, longWindow.WindowDurationMins);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(0, 100)]
    [InlineData(28, 72)]
    [InlineData(120, 0)]
    public void Parse_ClampsRemainingPercent(double usedPercent, int expectedRemaining)
    {
        using var document = JsonDocument.Parse($$"""
        {
          "result": {
            "rateLimits": {
              "primary": { "usedPercent": {{usedPercent}}, "windowDurationMins": 300 }
            }
          }
        }
        """);

        var (shortWindow, _) = RateLimitParser.Parse(document.RootElement);

        Assert.NotNull(shortWindow);
        Assert.Equal(expectedRemaining, shortWindow.RemainingPercent);
    }

    [Theory]
    [InlineData(300, "5h")]
    [InlineData(1440, "1d")]
    [InlineData(10080, "7d")]
    [InlineData(45, "45m")]
    public void FormatWindowLabel_UsesDuration(int minutes, string expected)
    {
        Assert.Equal(expected, RateLimitFormatter.FormatWindowLabel(minutes));
    }
}
