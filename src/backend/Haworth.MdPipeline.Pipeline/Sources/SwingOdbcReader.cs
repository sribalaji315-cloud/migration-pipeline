using System.Data.Common;
using System.Data.Odbc;
using System.Globalization;
using System.Runtime.CompilerServices;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>
/// Read-only Swing reader for GSST00 (BOM) and GSAR00 (item master). Only validated SELECT statements are issued;
/// the ODBC account must be a read-only user. Failures surface as run failures, never as a fallback to another source.
/// </summary>
public sealed class SwingOdbcReader(IOptions<SwingOptions> options) : ISwingBomReader, ISwingItemReader, ISwingHealthProbe
{
    private readonly SwingOptions swing = options.Value;

    public SourceExtract ReadBom(CancellationToken ct) =>
        new(SwingContract.BomTable, SwingContract.Gsst00Columns, ReadAsync(Render(swing.BomSql), SwingContract.Gsst00Columns, ct));

    public SourceExtract ReadItems(CancellationToken ct) =>
        new(SwingContract.ItemTable, SwingContract.Gsar00Columns, ReadAsync(Render(swing.ItemSql), SwingContract.Gsar00Columns, ct));

    public async Task<SourceHealth> CheckAsync(CancellationToken ct)
    {
        try
        {
            Render(swing.BomSql);
            Render(swing.ItemSql);
            await using var connection = new OdbcConnection(swing.OdbcConnectionString);
            await connection.OpenAsync(ct);
            return new SourceHealth("Swing", true, $"ODBC connection opened (library {swing.Library}).");
        }
        catch (Exception ex)
        {
            return new SourceHealth("Swing", false, ex.Message);
        }
    }

    private string Render(string template) => SqlGuard.RenderLibrary(template, swing.Library, swing.AllowedLibraries);

    private async IAsyncEnumerable<string?[]> ReadAsync(string sql, IReadOnlyList<string> columns, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var connection = new OdbcConnection(swing.OdbcConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = swing.CommandTimeoutSeconds;
        await using var reader = await command.ExecuteReaderAsync(ct);

        var ordinals = ResolveOrdinals(reader, columns);
        while (await reader.ReadAsync(ct))
        {
            var row = new string?[columns.Count];
            for (var i = 0; i < ordinals.Length; i++)
            {
                row[i] = ToRaw(reader.GetValue(ordinals[i]));
            }

            yield return row;
        }
    }

    private static int[] ResolveOrdinals(DbDataReader reader, IReadOnlyList<string> columns)
    {
        var available = Enumerable.Range(0, reader.FieldCount)
            .ToDictionary(i => reader.GetName(i).Trim(), i => i, StringComparer.OrdinalIgnoreCase);
        var missing = columns.Where(c => !available.ContainsKey(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Swing extract is missing columns: {string.Join(", ", missing)}.");
        }

        return columns.Select(c => available[c]).ToArray();
    }

    /// <summary>Raw text representation; character data keeps padding, numbers use invariant culture.</summary>
    internal static string? ToRaw(object? value) => value switch
    {
        null or DBNull => null,
        string s => s,
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}
