using System.Globalization;
using System.Text.Json;
using Haworth.MdPipeline.Core.Interfaces;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>
/// Lenient Migration Tool payload parsing: the API contract is not formally fixed, so values are read by name with type
/// tolerance (text/number/bool, Unix-seconds or ISO timestamps) instead of strict deserialization.
/// </summary>
public static class MtPayload
{
    /// <summary>Accepts a bare array or an object wrapping it in "items"/"data"/"results".</summary>
    public static IEnumerable<JsonElement> Rows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        foreach (var name in new[] { "items", "data", "results" })
        {
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array)
            {
                return list.EnumerateArray();
            }
        }

        throw new InvalidOperationException($"Unexpected Migration Tool response: expected a JSON array, got {root.ValueKind}.");
    }

    public static MtMappingRow Mapping(JsonElement row, string? fallbackItemId = null)
    {
        var prediction = row.TryGetProperty("prediction", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
        var itemId = Text(row, "itemId", "legacyItemId") ?? fallbackItemId
            ?? throw new InvalidOperationException("Migration Tool mapping row has no itemId.");

        return new MtMappingRow(
            itemId.Trim(),
            Text(row, "legacyFeatureId"),
            Text(row, "legacyValue"),
            Text(row, "newAttributeId"),
            Text(row, "newValue"),
            Text(row, "attributeType"),
            Text(row, "condition"),
            Text(row, "simplifiedLegacyCondition"),
            Text(row, "targetCondition"),
            Text(row, "formula"),
            Text(row, "mappedFrom"),
            Text(row, "valueStatus"),
            Text(row, "feasibility"),
            Bool(row, "legacyReachable"),
            Text(row, "inherited"),
            Text(prediction, "status") ?? Text(row, "predictionStatus"),
            Text(prediction, "confidence") ?? Text(row, "predictionConfidence"),
            Text(prediction, "matchType"),
            Text(prediction, "evidence") ?? Text(row, "predictionEvidence"),
            Text(row, "signedOnByUsername", "signedOnByUserId"),
            Timestamp(row, "signedOnAt"),
            Timestamp(row, "updatedAt"),
            row.GetRawText());
    }

    public static MtItemHeader Item(JsonElement row) =>
        new(
            (Text(row, "itemId", "id") ?? throw new InvalidOperationException("Migration Tool item header has no itemId.")).Trim(),
            Text(row, "description"),
            Text(row, "category"),
            Text(row, "productType"),
            Text(row, "priority"),
            Bool(row, "approvedForMigration"),
            row.GetRawText());

    /// <summary>First present, non-null property as text; numbers/bools as invariant text, objects/arrays as raw JSON.</summary>
    public static string? Text(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var v))
            {
                continue;
            }

            switch (v.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    continue;
                case JsonValueKind.String:
                    return v.GetString();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                default:
                    return v.GetRawText();
            }
        }

        return null;
    }

    /// <summary>true/false, or text yes/no/true/false/y/n/1/0; anything else is null (unknown, never defaulted).</summary>
    public static bool? Bool(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v)
            ? v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => v.TryGetDecimal(out var n) ? n != 0 : null,
                JsonValueKind.String => v.GetString()?.Trim().ToLowerInvariant() switch
                {
                    "yes" or "y" or "true" or "1" => true,
                    "no" or "n" or "false" or "0" => false,
                    _ => null
                },
                _ => null
            }
            : null;

    /// <summary>Unix seconds (or milliseconds when &gt; 1e11), numeric text, or ISO-8601 text.</summary>
    public static DateTimeOffset? Timestamp(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v))
        {
            return null;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var number))
        {
            return FromUnix(number);
        }

        if (v.ValueKind == JsonValueKind.String)
        {
            var s = v.GetString();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                return FromUnix(numeric);
            }

            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return null;
    }

    private static DateTimeOffset FromUnix(double value) =>
        value > 1e11
            ? DateTimeOffset.UnixEpoch.AddMilliseconds(value)
            : DateTimeOffset.UnixEpoch.AddTicks((long)Math.Round(value * TimeSpan.TicksPerSecond));
}
