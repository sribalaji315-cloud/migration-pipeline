using System.Runtime.CompilerServices;
using System.Text;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>Minimal RFC 4180 reader. Unquoted empty fields are NULL; quoted empty fields are empty strings; padding is preserved.</summary>
public static class CsvTable
{
    public static async IAsyncEnumerable<string?[]> ReadAsync(
        string path, IReadOnlyList<string> columns, [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        var header = await ReadRecordAsync(reader, ct)
            ?? throw new InvalidOperationException($"Fixture '{path}' is empty.");
        var index = header.Select((name, i) => (name: name?.Trim() ?? string.Empty, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);
        var missing = columns.Where(c => !index.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Fixture '{path}' is missing columns: {string.Join(", ", missing)}.");
        }

        var ordinals = columns.Select(c => index[c]).ToArray();
        while (await ReadRecordAsync(reader, ct) is { } record)
        {
            if (record.Length == 1 && record[0] is null)
            {
                continue;
            }

            yield return ordinals.Select(o => o < record.Length ? record[o] : null).ToArray();
        }
    }

    private static async Task<string?[]?> ReadRecordAsync(StreamReader reader, CancellationToken ct)
    {
        var line = await reader.ReadLineAsync(ct);
        if (line is null)
        {
            return null;
        }

        var fields = new List<string?>();
        var field = new StringBuilder();
        var quoted = false;
        var wasQuoted = false;
        var i = 0;
        while (true)
        {
            if (i >= line.Length)
            {
                if (quoted)
                {
                    field.Append('\n');
                    line = await reader.ReadLineAsync(ct) ?? throw new InvalidOperationException("Unterminated quoted CSV field.");
                    i = 0;
                    continue;
                }

                fields.Add(field.Length == 0 && !wasQuoted ? null : field.ToString());
                return fields.ToArray();
            }

            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i += 2;
                    continue;
                }

                if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"' && field.Length == 0)
            {
                quoted = true;
                wasQuoted = true;
            }
            else if (c == ',')
            {
                fields.Add(field.Length == 0 && !wasQuoted ? null : field.ToString());
                field.Clear();
                wasQuoted = false;
            }
            else
            {
                field.Append(c);
            }

            i++;
        }
    }
}
