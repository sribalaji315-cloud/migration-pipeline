using System.Reflection;
using System.Text.RegularExpressions;

namespace Haworth.MdPipeline.Pipeline.Duck;

/// <summary>
/// Loads embedded step SQL. Tokens written as {{name}} are replaced with escaped SQL string-literal content
/// (used for Parquet paths only; data values are passed through cfg tables, never interpolated).
/// </summary>
public static partial class SqlScriptLoader
{
    private static readonly Assembly Assembly = typeof(SqlScriptLoader).Assembly;

    public static string Load(string fileName, IReadOnlyDictionary<string, string>? literals = null)
    {
        var resource = $"Haworth.MdPipeline.Pipeline.Sql.{fileName}";
        using var stream = Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"SQL script '{fileName}' is not embedded.");
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        sql = Token().Replace(sql, m =>
        {
            var key = m.Groups[1].Value;
            if (literals is null || !literals.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException($"SQL script '{fileName}' needs token '{key}'.");
            }

            return value.Replace("'", "''", StringComparison.Ordinal);
        });

        return sql;
    }

    public static string SqlPath(string path) => Path.GetFullPath(path).Replace('\\', '/');

    [GeneratedRegex(@"\{\{([a-z_]+)\}\}")]
    private static partial Regex Token();
}
