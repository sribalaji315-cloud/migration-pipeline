using System.Globalization;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Core.Rules;

public sealed record TypeCodeEntry(int Code, SourceType Type);

public sealed record PseudoFlagEntry(string Raw, bool IsPseudo);

public sealed record OpenEndedDatePattern(int Year, int Month, int Day);

/// <summary>
/// Explicit, validated Swing interpretation rules. Anything not covered stays UNKNOWN / unresolved and raises an issue.
/// </summary>
public sealed record SwingInterpretation(
    IReadOnlyList<TypeCodeEntry> TypeCodes,
    IReadOnlyList<PseudoFlagEntry> PseudoFlags,
    IReadOnlyList<OpenEndedDatePattern> OpenEndedDateIn,
    IReadOnlyList<OpenEndedDatePattern> OpenEndedDateOut,
    int? TwoDigitYearPivot,
    bool DateOutInclusive)
{
    public static SwingInterpretation Create(
        IReadOnlyDictionary<string, string> typeCodes,
        IReadOnlyDictionary<string, bool> pseudoFlags,
        IEnumerable<string> openEndedDateIn,
        IEnumerable<string> openEndedDateOut,
        int? twoDigitYearPivot,
        bool dateOutInclusive)
    {
        var codes = typeCodes.Select(kv =>
        {
            if (!int.TryParse(kv.Key.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                throw new DomainValidationException($"Swing type code '{kv.Key}' is not an integer.");
            }

            var type = kv.Value.Trim().ToUpperInvariant() switch
            {
                "FIXED" => SourceType.Fixed,
                "CONFIGURABLE" => SourceType.Configurable,
                _ => throw new DomainValidationException($"Swing type code {code} maps to unsupported type '{kv.Value}'.")
            };
            return new TypeCodeEntry(code, type);
        }).ToList();

        if (codes.GroupBy(c => c.Code).Any(g => g.Count() > 1))
        {
            throw new DomainValidationException("Swing type codes contain duplicates.");
        }

        var pseudo = pseudoFlags.Select(kv =>
        {
            var raw = kv.Key.Trim();
            if (raw.Length == 0)
            {
                throw new DomainValidationException("Pseudo flag map keys must not be blank; blank flags stay unresolved.");
            }

            return new PseudoFlagEntry(raw, kv.Value);
        }).ToList();

        if (pseudo.GroupBy(p => p.Raw, StringComparer.Ordinal).Any(g => g.Count() > 1))
        {
            throw new DomainValidationException("Pseudo flag map contains duplicate keys after trimming.");
        }

        if (twoDigitYearPivot is < 0 or > 99)
        {
            throw new DomainValidationException("TwoDigitYearPivot must be between 0 and 99.");
        }

        return new SwingInterpretation(
            codes, pseudo, ParsePatterns(openEndedDateIn), ParsePatterns(openEndedDateOut), twoDigitYearPivot, dateOutInclusive);
    }

    /// <summary>Patterns are raw year/month/day component values, e.g. "0/0/0" or "9999/12/31".</summary>
    private static List<OpenEndedDatePattern> ParsePatterns(IEnumerable<string> patterns) =>
        patterns.Select(p =>
        {
            var parts = p.Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !parts.All(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                throw new DomainValidationException($"Open-ended date pattern '{p}' must be 'year/month/day' integers.");
            }

            return new OpenEndedDatePattern(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture));
        }).Distinct().ToList();
}
