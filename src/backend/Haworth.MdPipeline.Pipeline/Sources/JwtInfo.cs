using System.Text;
using System.Text.Json;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>Reads non-secret claims (expiry) from a JWT without validating it. The token itself is never logged or returned.</summary>
public static class JwtInfo
{
    public static string Normalize(string token)
    {
        var trimmed = token.Trim();
        return trimmed.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? trimmed[7..].Trim() : trimmed;
    }

    /// <summary>Expiry from the "exp" claim, or null if the token is not a readable JWT or has no expiry.</summary>
    public static DateTimeOffset? Expiry(string token)
    {
        var parts = Normalize(token).Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
