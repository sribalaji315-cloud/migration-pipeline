using System.Text.Json;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Api.Jobs;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Data;
using Microsoft.EntityFrameworkCore;

namespace Haworth.MdPipeline.Api.Services;

public sealed class RunService(
    MdPipelineDbContext db,
    AuditService audit,
    RunQueue queue,
    TimeProvider clock,
    SilverQueryService silver)
{
    public const int MaxLimit = 500;

    public async Task<RunDto> QueueSyncAsync(bool full, RunRequestDto? request, string actorId, CancellationToken ct)
    {
        var hasPublished = await db.BronzeVersions.AnyAsync(v => v.Status == BronzeVersionStatus.Published, ct);
        var run = NewRun(request, actorId);
        run.SyncRequested = true;
        run.SyncMode = full || !hasPublished ? SyncMode.Full : SyncMode.Incremental;
        return await QueueAsync(run, $"Sync ({run.SyncMode}) + analysis queued.", ct);
    }

    /// <summary>Full GSST00/GSAR00 extract; Migration Tool data is carried forward from the latest snapshot.</summary>
    public async Task<RunDto> QueueSwingImportAsync(RunRequestDto? request, string actorId, CancellationToken ct)
    {
        await EnsureNoActiveImportAsync(ct);
        var run = NewRun(request, actorId);
        run.SyncRequested = true;
        run.SyncMode = SyncMode.Full;
        run.SyncScope = SyncScope.SwingOnly;
        return await QueueAsync(run, "Swing import (GSST00 + GSAR00) + analysis queued.", ct);
    }

    /// <summary>
    /// Migration Tool mappings + item headers; Swing data is carried forward. With top BOMs only their explosions are
    /// fetched (per-item calls) and every other item's mappings are kept from the previous snapshot.
    /// </summary>
    public async Task<RunDto> QueueMigrationToolImportAsync(bool full, MigrationToolImportRequestDto? request, string actorId, CancellationToken ct)
    {
        await EnsureNoActiveImportAsync(ct);
        var previous = await db.BronzeVersions
            .Where(v => v.Status == BronzeVersionStatus.Published)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct);
        if (previous is null || previous.SwingBomHash is null)
        {
            throw new DomainValidationException("Import Swing data first: the Migration Tool import reuses the Swing BOM of the latest snapshot.");
        }

        var topBoms = (request?.TopBoms ?? [])
            .Select(b => b?.Trim() ?? string.Empty)
            .Where(b => b.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (topBoms.Count > MaxScopeTopBoms)
        {
            throw new DomainValidationException($"Select at most {MaxScopeTopBoms} BOMs per import.");
        }

        if (topBoms.Count > 0)
        {
            var preview = await silver.ScopePreviewAsync(topBoms, ct);
            if (preview.Unknown.Count > 0)
            {
                throw new DomainValidationException($"Unknown in the latest Swing analysis: {string.Join(", ", preview.Unknown.Take(20))}.");
            }
        }

        var run = NewRun(request?.ToRunRequest(), actorId);
        run.SyncRequested = true;
        run.SyncMode = full || topBoms.Count > 0 ? SyncMode.Full : SyncMode.Incremental;
        run.SyncScope = SyncScope.MigrationToolOnly;
        run.ScopeTopBomsJson = topBoms.Count > 0 ? JsonSerializer.Serialize(topBoms) : null;
        var summary = topBoms.Count > 0
            ? $"Migration Tool import for {topBoms.Count} BOM(s) ({string.Join(", ", topBoms.Take(5))}{(topBoms.Count > 5 ? ", …" : string.Empty)}) + analysis queued."
            : $"Migration Tool import ({run.SyncMode}) + analysis queued.";
        return await QueueAsync(run, summary, ct);
    }

    public const int MaxScopeTopBoms = 1000;

    private async Task EnsureNoActiveImportAsync(CancellationToken ct)
    {
        if (await db.PipelineRuns.AnyAsync(r => r.SyncRequested
                && (r.Status == RunStatus.Queued || r.Status == RunStatus.Syncing || r.Status == RunStatus.Running), ct))
        {
            throw new ConflictException("A source import is already queued or running. Wait for it to finish.");
        }
    }

    public async Task<RunDto> QueueAnalysisAsync(RunRequestDto? request, string actorId, CancellationToken ct)
    {
        var bronze = await db.BronzeVersions
            .Where(v => v.Status == BronzeVersionStatus.Published)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct)
            ?? throw new DomainValidationException("No published Bronze version exists. Run a sync first.");
        if (!Directory.Exists(bronze.Path))
        {
            throw new DomainValidationException($"Bronze v{bronze.Version} folder is missing ({bronze.Path}).");
        }

        var run = NewRun(request, actorId);
        run.BronzeVersion = bronze.Version;
        return await QueueAsync(run, $"Analysis queued on Bronze v{bronze.Version}.", ct);
    }

    public async Task<PagedResult<RunDto>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = Page(limit, offset);
        var total = await db.PipelineRuns.CountAsync(ct);
        var runs = await db.PipelineRuns.Include(r => r.Steps)
            .OrderByDescending(r => r.QueuedAt).ThenByDescending(r => r.RunId)
            .Skip(offset).Take(limit)
            .ToListAsync(ct);
        return new PagedResult<RunDto>(runs.Select(r => r.ToDto()).ToList(), total, limit, offset);
    }

    public async Task<RunDto> GetAsync(string runId, CancellationToken ct)
    {
        var run = await db.PipelineRuns.Include(r => r.Steps).SingleOrDefaultAsync(r => r.RunId == runId, ct)
            ?? throw new NotFoundException($"Run '{runId}' was not found.");
        return run.ToDto();
    }

    public async Task<IReadOnlyList<BronzeVersionDto>> BronzeVersionsAsync(CancellationToken ct) =>
        (await db.BronzeVersions.OrderByDescending(v => v.Version).ToListAsync(ct)).Select(v => v.ToDto()).ToList();

    /// <summary>The queue is in-memory, so runs left non-terminal by a restart can never finish.</summary>
    public async Task FailInterruptedRunsAsync(CancellationToken ct)
    {
        var interrupted = await db.PipelineRuns
            .Where(r => r.Status == RunStatus.Queued || r.Status == RunStatus.Syncing || r.Status == RunStatus.Running)
            .ToListAsync(ct);
        foreach (var run in interrupted)
        {
            run.Status = RunStatus.Failed;
            run.FinishedAt = clock.GetUtcNow().UtcDateTime;
            run.ErrorMessage = run.SilverPath is not null && !File.Exists(run.SilverPath)
                ? "Interrupted by application restart; Silver output is missing."
                : "Interrupted by application restart.";
            audit.Write(nameof(PipelineRun), run.RunId, "Failed", "system", run.ErrorMessage);
        }

        foreach (var version in await db.BronzeVersions.Where(v => v.Status == BronzeVersionStatus.Writing).ToListAsync(ct))
        {
            version.Status = BronzeVersionStatus.Failed;
        }

        await db.SaveChangesAsync(ct);
    }

    private PipelineRun NewRun(RunRequestDto? request, string actorId)
    {
        var now = clock.GetUtcNow();
        var policy = request?.BomTypePolicy is { } p ? BomTypePolicy.Create(p.Mode, p.Types) : BomTypePolicy.AllTypesUnfiltered;
        return new PipelineRun
        {
            RunId = $"A-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
            RunType = RunType.Analysis,
            EffectiveDate = request?.EffectiveDate ?? DateOnly.FromDateTime(now.LocalDateTime),
            BomTypePolicyJson = policy.ToJson(),
            Status = RunStatus.Queued,
            StartedByUserId = actorId,
            QueuedAt = now.UtcDateTime
        };
    }

    private async Task<RunDto> QueueAsync(PipelineRun run, string summary, CancellationToken ct)
    {
        db.PipelineRuns.Add(run);
        audit.Write(nameof(PipelineRun), run.RunId, "Queued", run.StartedByUserId, summary);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(run.RunId, ct);
        return run.ToDto();
    }

    public static (int Limit, int Offset) Page(int limit, int offset) =>
        (Math.Clamp(limit <= 0 ? 50 : limit, 1, MaxLimit), Math.Max(0, offset));
}
