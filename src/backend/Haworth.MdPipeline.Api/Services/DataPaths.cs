using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Api.Services;

/// <summary>Resolves data-layer folders. SQLite stores these paths; DuckDB/Parquet hold the data.</summary>
public sealed class DataPaths(IOptions<PipelineOptions> options)
{
    public string DataDir => Path.GetFullPath(options.Value.DataDir);

    public string BronzeDir(int version) => Path.Combine(DataDir, "bronze", $"v{version}");

    public string BronzeStagingDir(int version) => Path.Combine(DataDir, "bronze", $"v{version}.tmp");

    public string ReferenceDir(int version) => Path.Combine(DataDir, "reference", $"bs-{version}");

    public string ReferenceStagingDir(int version) => Path.Combine(DataDir, "reference", $"bs-{version}.tmp");

    public string SilverPath(string runId) => Path.Combine(DataDir, "runs", runId, "silver.duckdb");

    public string GoldPath(string runId) => Path.Combine(DataDir, "runs", runId, "gold.duckdb");

    public string WaveExportDir(string waveId, string runId) => Path.Combine(DataDir, "exports", waveId, runId);

    /// <summary>
    /// Publishes a staging folder. Retries briefly because Windows (antivirus/indexer) may still hold a just-written file,
    /// which makes Directory.Move fail transiently with access denied.
    /// </summary>
    public static async Task PublishDirectoryAsync(string staging, string target, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(staging, target);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 10)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
        }
    }
}
