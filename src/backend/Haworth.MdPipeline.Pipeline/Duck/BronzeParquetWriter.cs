using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Options;

namespace Haworth.MdPipeline.Pipeline.Duck;

public sealed record BronzeColumn(string Name, string DuckType = "VARCHAR");

public sealed record BronzeTableSpec(string Table, IReadOnlyList<BronzeColumn> Columns)
{
    public static BronzeTableSpec FromExtract(string table, SourceExtract extract) =>
        new(table, extract.Columns.Select(c => new BronzeColumn(c)).ToList());
}

public sealed record BronzeMeta(int BronzeVersion, DateTime ExtractedAt, string Source, string SourceTable);

public sealed record BronzeWriteResult(long Rows, string ContentHash, DateTime ExtractedAt);

/// <summary>
/// Writes immutable Bronze Parquet: raw source columns plus _source, _source_table, _extracted_at, _bronze_version, _row_hash.
/// DDL/COPY here is generated from source contracts (infrastructure); step logic stays in Sql/*.sql.
/// </summary>
public sealed class BronzeParquetWriter(PipelineOptions options)
{
    public async Task<BronzeWriteResult> WriteAsync(
        BronzeTableSpec spec,
        IAsyncEnumerable<object?[]> rows,
        BronzeMeta meta,
        string outputPath,
        string? partitionBy,
        CancellationToken ct)
    {
        DuckDbExecutor.EnsureIdentifier(spec.Table);
        foreach (var column in spec.Columns)
        {
            DuckDbExecutor.EnsureIdentifier(column.Name.ToLowerInvariant());
        }

        var stagingDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outputPath))!, "_staging");
        Directory.CreateDirectory(stagingDir);
        var stagingDb = Path.Combine(stagingDir, $"{spec.Table}_{Guid.NewGuid():N}.duckdb");
        var hash = new ContentHash();

        try
        {
            long count;
            using (var duck = DuckDbExecutor.Open(stagingDb, options))
            {
                var columns = string.Join(", ", spec.Columns.Select(c => $"\"{c.Name}\" {c.DuckType}"));
                duck.ExecuteScript(
                    $"CREATE TABLE {spec.Table} ({columns}, _source VARCHAR, _source_table VARCHAR, _extracted_at TIMESTAMP, _bronze_version INTEGER, _row_hash VARCHAR)");

                count = await duck.AppendAsync(spec.Table, WithMeta(rows, meta, hash, ct), ct);

                var target = SqlScriptLoader.SqlPath(outputPath).Replace("'", "''", StringComparison.Ordinal);
                var partition = partitionBy is null ? string.Empty : $", PARTITION_BY (\"{partitionBy}\"), OVERWRITE_OR_IGNORE";
                duck.ExecuteScript($"COPY (SELECT * FROM {spec.Table} ORDER BY _row_hash) TO '{target}' (FORMAT PARQUET, COMPRESSION ZSTD{partition})");
            }

            return new BronzeWriteResult(count, hash.Finish(count), meta.ExtractedAt);
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(stagingDir, Path.GetFileNameWithoutExtension(stagingDb) + "*"))
            {
                File.Delete(file);
            }

            if (!Directory.EnumerateFileSystemEntries(stagingDir).Any())
            {
                Directory.Delete(stagingDir);
            }
        }
    }

    public static async IAsyncEnumerable<object?[]> FromExtract(SourceExtract extract, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var row in extract.Rows.WithCancellation(ct))
        {
            yield return row.Cast<object?>().ToArray();
        }
    }

    private static async IAsyncEnumerable<object?[]> WithMeta(
        IAsyncEnumerable<object?[]> rows, BronzeMeta meta, ContentHash hash, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var row in rows.WithCancellation(ct))
        {
            var rowHash = RowHash(row);
            hash.Add(rowHash);
            var values = new object?[row.Length + 5];
            Array.Copy(row, values, row.Length);
            values[row.Length] = meta.Source;
            values[row.Length + 1] = meta.SourceTable;
            values[row.Length + 2] = meta.ExtractedAt;
            values[row.Length + 3] = meta.BronzeVersion;
            values[row.Length + 4] = Convert.ToHexStringLower(rowHash);
            yield return values;
        }
    }

    private static byte[] RowHash(object?[] row)
    {
        var builder = new StringBuilder();
        foreach (var value in row)
        {
            builder.Append(value switch
            {
                null => "\u0000",
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            });
            builder.Append('\u001f');
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    /// <summary>Order-independent, duplicate-sensitive content hash (row count + two 64-bit lane sums).</summary>
    private sealed class ContentHash
    {
        private ulong lane1;
        private ulong lane2;

        public void Add(byte[] rowHash)
        {
            unchecked
            {
                lane1 += BinaryPrimitives.ReadUInt64BigEndian(rowHash.AsSpan(0, 8));
                lane2 += BinaryPrimitives.ReadUInt64BigEndian(rowHash.AsSpan(8, 8));
            }
        }

        public string Finish(long count) => $"{count:x}-{lane1:x16}{lane2:x16}";
    }
}
