using System.Text.Json;
using CodexBall.Core.Models;

namespace CodexBall.Core.Codex;

public static class RateLimitParser
{
    public static (RateLimitWindow? ShortWindow, RateLimitWindow? LongWindow) Parse(JsonElement response)
    {
        var root = response.TryGetProperty("result", out var result) ? result : response;
        JsonElement limitsRoot;

        if (root.TryGetProperty("rateLimitsByLimitId", out var byLimitId)
            && byLimitId.ValueKind == JsonValueKind.Object
            && byLimitId.TryGetProperty("codex", out var codexLimit))
        {
            limitsRoot = codexLimit;
        }
        else if (root.TryGetProperty("rateLimits", out var rateLimits))
        {
            limitsRoot = rateLimits;
        }
        else
        {
            limitsRoot = root;
        }

        var windows = new[] { TryParseWindow(limitsRoot, "primary"), TryParseWindow(limitsRoot, "secondary") }
            .Where(window => window is not null)
            .Cast<RateLimitWindow>()
            .OrderBy(window => window.WindowDurationMins)
            .ToList();

        return windows.Count switch
        {
            0 => (null, null),
            1 => (windows[0], null),
            _ => (windows[0], windows[^1])
        };
    }

    private static RateLimitWindow? TryParseWindow(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var window) || window.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var usedPercent = GetDouble(window, "usedPercent");
        var duration = GetInt(window, "windowDurationMins");
        var resetsAt = GetLong(window, "resetsAt");

        if (usedPercent is null || duration is null)
        {
            return null;
        }

        return new RateLimitWindow(
            usedPercent.Value,
            duration.Value,
            resetsAt is null ? null : DateTimeOffset.FromUnixTimeSeconds(resetsAt.Value));
    }

    private static double? GetDouble(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetDouble(out var value) ? value : null;

    private static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value) ? value : null;

    private static long? GetLong(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value) ? value : null;
}
