using System.Text.Json;
using System.Text.Json.Serialization;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Rules;

/// <summary>
/// BOM-type filter pinned with each run and wave. BOM-type meanings are unconfirmed, so the default keeps every code.
/// </summary>
public sealed record BomTypePolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public BomTypePolicyMode Mode { get; init; }
    public IReadOnlyList<string> Types { get; init; } = [];

    public static BomTypePolicy AllTypesUnfiltered { get; } = new() { Mode = BomTypePolicyMode.AllTypesUnfiltered };

    public static BomTypePolicy Create(BomTypePolicyMode mode, IEnumerable<string>? types)
    {
        var normalized = (types ?? [])
            .Select(t => t?.Trim() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return mode switch
        {
            BomTypePolicyMode.AllTypesUnfiltered when normalized.Count > 0 =>
                throw new DomainValidationException("AllTypesUnfiltered policy must not list BOM types."),
            BomTypePolicyMode.IncludeOnly when normalized.Count == 0 =>
                throw new DomainValidationException("IncludeOnly policy requires at least one BOM type."),
            _ => new BomTypePolicy { Mode = mode, Types = normalized }
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static BomTypePolicy FromJson(string json)
    {
        var parsed = JsonSerializer.Deserialize<BomTypePolicy>(json, JsonOptions)
            ?? throw new DomainValidationException("BOM-type policy is empty.");
        return Create(parsed.Mode, parsed.Types);
    }

    public bool Equals(BomTypePolicy? other) =>
        other is not null && Mode == other.Mode && Types.SequenceEqual(other.Types, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Mode, string.Join('\u001f', Types));
}
