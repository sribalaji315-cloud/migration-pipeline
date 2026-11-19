using System.Text.RegularExpressions;
using Haworth.MdPipeline.Core.Domain;

namespace Haworth.MdPipeline.Core.Rules;

/// <summary>Guards configured source SQL: Swing access is read-only, single statement, SELECT only.</summary>
public static partial class SqlGuard
{
    public static void EnsureSelectOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new DomainValidationException("Configured source SQL is empty.");
        }

        var trimmed = sql.Trim();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || (trimmed.Length > 6 && !char.IsWhiteSpace(trimmed[6])))
        {
            throw new DomainValidationException("Configured source SQL must start with SELECT.");
        }

        if (trimmed.Contains(';') || trimmed.Contains("--") || trimmed.Contains("/*"))
        {
            throw new DomainValidationException("Configured source SQL must be a single statement without comments or ';'.");
        }

        if (ForbiddenKeyword().IsMatch(trimmed))
        {
            throw new DomainValidationException("Configured source SQL contains a forbidden keyword.");
        }
    }

    /// <summary>Validates the library/schema against an explicit allowlist and substitutes it into the template.</summary>
    public static string RenderLibrary(string sqlTemplate, string library, IReadOnlyCollection<string> allowedLibraries)
    {
        if (string.IsNullOrWhiteSpace(library) || !Identifier().IsMatch(library))
        {
            throw new DomainValidationException($"Swing library '{library}' is not a valid identifier.");
        }

        if (!allowedLibraries.Contains(library, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainValidationException($"Swing library '{library}' is not in Swing:AllowedLibraries.");
        }

        var rendered = sqlTemplate.Replace("{library}", library, StringComparison.Ordinal);
        EnsureSelectOnly(rendered);
        return rendered;
    }

    [GeneratedRegex(@"^[A-Za-z_#@$][A-Za-z0-9_#@$]{0,127}$")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|GRANT|REVOKE|CALL|EXEC|EXECUTE|INTO|LOCK)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenKeyword();
}
