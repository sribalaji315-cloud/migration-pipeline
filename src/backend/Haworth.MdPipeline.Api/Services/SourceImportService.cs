using System.Data.Common;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Api.Services;

/// <summary>Source status and connection tests for the Admin import panels. Credentials are never returned.</summary>
public sealed class SourceImportService(
    MdPipelineDbContext db,
    ISwingHealthProbe swingProbe,
    IMigrationToolClient migrationTool,
    IOptions<SwingOptions> swingOptions,
    IOptions<MigrationToolOptions> mtOptions,
    IOptions<PipelineOptions> pipelineOptions)
{
    public async Task<SwingSourceDto> GetSwingAsync(CancellationToken ct)
    {
        var swing = swingOptions.Value;
        var (latest, active, last) = await StatusAsync([SyncScope.All, SyncScope.SwingOnly], ct);
        return new SwingSourceDto(
            swing.Enabled ? "Odbc" : "Fixture",
            swing.Enabled ? DataSourceName(swing.OdbcConnectionString) : Path.Combine(pipelineOptions.Value.FixturesDir, "swing"),
            swing.Enabled ? swing.Library : string.Empty,
            latest, active, last);
    }

    public async Task<MigrationToolSourceDto> GetMigrationToolAsync(CancellationToken ct)
    {
        var mt = mtOptions.Value;
        var (latest, active, last) = await StatusAsync([SyncScope.All, SyncScope.MigrationToolOnly], ct);
        var authMode = mt.UseFixtures ? "None" : mt.HasBearerToken ? "BearerToken" : "Login";
        return new MigrationToolSourceDto(
            mt.UseFixtures ? "Fixture" : "Rest",
            mt.UseFixtures ? Path.Combine(pipelineOptions.Value.FixturesDir, "mt") : mt.BaseUrl,
            authMode,
            authMode == "Login" ? mt.User : string.Empty,
            authMode == "BearerToken" ? JwtInfo.Expiry(mt.BearerToken) : null,
            mt.UseFixtures || mt.UseBulkEndpoint,
            latest, active, last);
    }

    public async Task<HealthCheckDto> TestSwingAsync(CancellationToken ct) => ToDto(await swingProbe.CheckAsync(ct));

    public async Task<HealthCheckDto> TestMigrationToolAsync(CancellationToken ct) => ToDto(await migrationTool.CheckAsync(ct));

    /// <summary>Latest snapshot, any active import (imports are serialized) and the last finished import that extracted this source.</summary>
    private async Task<(BronzeVersionDto? Latest, RunDto? Active, RunDto? Last)> StatusAsync(SyncScope[] scopes, CancellationToken ct)
    {
        var latest = await db.BronzeVersions
            .Where(v => v.Status == BronzeVersionStatus.Published)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct);
        var imports = db.PipelineRuns.Include(r => r.Steps).Where(r => r.SyncRequested);
        var active = await imports
            .Where(r => r.Status == RunStatus.Queued || r.Status == RunStatus.Syncing || r.Status == RunStatus.Running)
            .OrderByDescending(r => r.QueuedAt)
            .FirstOrDefaultAsync(ct);
        var last = await imports
            .Where(r => (r.Status == RunStatus.Completed || r.Status == RunStatus.Failed) && scopes.Contains(r.SyncScope))
            .OrderByDescending(r => r.QueuedAt)
            .FirstOrDefaultAsync(ct);
        return (latest?.ToDto(), active?.ToDto(), last?.ToDto());
    }

    private static HealthCheckDto ToDto(SourceHealth health) => new(health.Name, health.Healthy, health.Detail);

    /// <summary>Only the DSN / system / driver name is exposed; user and password are never returned.</summary>
    private static string DataSourceName(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (var key in new[] { "DSN", "SYSTEM", "Driver" })
            {
                if (builder.TryGetValue(key, out var value) && value is string s && s.Length > 0)
                {
                    return $"{key}={s}";
                }
            }
        }
        catch (ArgumentException)
        {
        }

        return "(not configured)";
    }
}
