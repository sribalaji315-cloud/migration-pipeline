using System.Security.Cryptography;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Duck;

namespace Haworth.MdPipeline.Pipeline.Sources;

public sealed record ReferenceFileResult(string Kind, string FileName, string BronzeTable, long Rows, string Sha256);

/// <summary>Converts the five uploaded Bluestar workbooks into versioned reference Parquet (raw values, untrimmed).</summary>
public sealed class BluestarReferenceImporter(IBluestarReferenceReader reader, BronzeParquetWriter writer)
{
    public async Task<IReadOnlyList<ReferenceFileResult>> ImportAsync(
        int version, string targetDir, IReadOnlyDictionary<BluestarFileKind, Stream> files, CancellationToken ct)
    {
        var missing = BluestarContract.Files.Where(f => !files.ContainsKey(f.Kind)).Select(f => f.FileName).ToList();
        if (missing.Count > 0)
        {
            throw new DomainValidationException($"Bluestar upload is missing: {string.Join(", ", missing)}.");
        }

        Directory.CreateDirectory(Path.Combine(targetDir, "raw"));
        var results = new List<ReferenceFileResult>();
        foreach (var spec in BluestarContract.Files)
        {
            var stream = files[spec.Kind];
            var rawPath = Path.Combine(targetDir, "raw", spec.FileName);
            await using (var raw = File.Create(rawPath))
            {
                await stream.CopyToAsync(raw, ct);
            }

            string sha;
            await using (var raw = File.OpenRead(rawPath))
            {
                sha = Convert.ToHexStringLower(await SHA256.HashDataAsync(raw, ct));
            }

            await using var workbook = File.OpenRead(rawPath);
            var extract = reader.Read(spec, workbook);
            var written = await writer.WriteAsync(
                BronzeTableSpec.FromExtract(spec.BronzeTable, extract),
                BronzeParquetWriter.FromExtract(extract, ct),
                new BronzeMeta(version, DateTime.UtcNow, "Bluestar", spec.FileName),
                Path.Combine(targetDir, $"{spec.BronzeTable}.parquet"),
                partitionBy: null,
                ct);
            results.Add(new ReferenceFileResult(spec.Kind.ToString(), spec.FileName, spec.BronzeTable, written.Rows, sha));
        }

        return results;
    }
}
