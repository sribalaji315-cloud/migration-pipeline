using System.Diagnostics;
using System.Text.Json;
using Haworth.MdPipeline.Api.Services;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Api.Jobs;

/// <summary>
/// Executes one queued run: optional source sync into a staged Bronze version (published only when every extract succeeds),
/// then the Silver steps. Records every step and state change in SQLite.
/// </summary>
public sealed class PipelineRunner(
    MdPipelineDbContext db,
    AuditService audit,
    DataPaths paths,
    AnalysisStateService analysisState,
    IOptions<SwingOptions> swingOptions,
    TimeProvider clock,
    IServiceProvider services,
    ILogger<PipelineRunner> logger)
{
    private int sequence;

    public async Task ExecuteAsync(string runId, CancellationToken ct)
    {
        var run = await db.PipelineRuns.Include(r => r.Steps).SingleAsync(r => r.RunId == runId, ct);
        if (run.Status != RunStatus.Queued)
        {
            return;
        }

        BronzeVersion? reserved = null;
        BronzeVersion? previous = null;
        string? staging = null;
        try
        {
            run.StartedAt = Now();
            sequence = run.Steps.Count;
            if (run.RunType == RunType.WaveExport)
            {
                await ExportWaveAsync(run, ct);
                return;
            }

            BronzeTarget? target = null;

            if (run.SyncRequested)
            {
                Transition(run, RunStatus.Syncing);
                previous = await db.BronzeVersions
                    .Where(v => v.Status == BronzeVersionStatus.Published)
                    .OrderByDescending(v => v.Version)
                    .FirstOrDefaultAsync(ct);
                var next = (await db.BronzeVersions.MaxAsync(v => (int?)v.Version, ct) ?? 0) + 1;
                reserved = new BronzeVersion
                {
                    Version = next,
                    Path = paths.BronzeDir(next),
                    Mode = run.SyncMode ?? SyncMode.Full,
                    SyncedAt = Now(),
                    Status = BronzeVersionStatus.Writing,
                    CreatedByRunId = run.RunId
                };
                db.BronzeVersions.Add(reserved);
                await db.SaveChangesAsync(ct);

                staging = paths.BronzeStagingDir(next);
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }

                Directory.CreateDirectory(staging);
                target = new BronzeTarget(next, staging, reserved.Mode, previous?.Path,
                    previous?.MaxUpdatedAt is { } max ? new DateTimeOffset(DateTime.SpecifyKind(max, DateTimeKind.Utc)) : null,
                    previous?.Version);
            }

            var ctx = new PipelineContext
            {
                RunId = run.RunId,
                EffectiveDate = run.EffectiveDate,
                BomTypePolicy = BomTypePolicy.FromJson(run.BomTypePolicyJson),
                Swing = swingOptions.Value.ToInterpretation(),
                SilverPath = paths.SilverPath(run.RunId),
                BronzeTarget = target,
                BronzeDir = target?.StagingDir,
                MtScopeTopBoms = run.ScopeTopBomsJson is null ? null : JsonSerializer.Deserialize<List<string>>(run.ScopeTopBomsJson)
            };

            if (reserved is not null && staging is not null)
            {
                // Swing first: the Migration Tool sync is scoped to GSST00 items.
                if (run.SyncScope == SyncScope.MigrationToolOnly)
                {
                    await RunStepAsync(run, services.GetRequiredService<CarryForwardSwingStep>(), ctx, ct);
                }
                else
                {
                    await RunStepAsync(run, services.GetRequiredService<SyncSwingBomStep>(), ctx, ct);
                    await RunStepAsync(run, services.GetRequiredService<SyncSwingItemStep>(), ctx, ct);
                }

                if (run.SyncScope == SyncScope.SwingOnly)
                {
                    await RunStepAsync(run, services.GetRequiredService<CarryForwardMigrationToolStep>(), ctx, ct);
                }
                else
                {
                    await RunStepAsync(run, services.GetRequiredService<SyncMigrationToolStep>(), ctx, ct);
                }

                await DataPaths.PublishDirectoryAsync(staging, reserved.Path, ct);
                staging = null;
                PublishBronze(reserved, ctx.Sync, previous);
                if (ctx.MtScopeTopBoms is { Count: > 0 })
                {
                    reserved.MtScopeTopBomsJson = run.ScopeTopBomsJson;
                    reserved.MtScopeItems = ctx.Sync.MtScopeItems;
                }
                run.BronzeVersion = reserved.Version;
                var mtSummary = reserved.MtCarriedFromVersion is { } from
                    ? $"Migration Tool data carried from v{from}"
                    : $"{reserved.MappingRows} mapping rows";
                var swingSummary = reserved.SwingCarriedFromVersion is { } swingFrom
                    ? $"Swing data carried from v{swingFrom}"
                    : $"{reserved.SwingBomRows} GSST00 rows, {reserved.SwingItems} GSAR00 rows";
                audit.Write(nameof(BronzeVersion), reserved.Version.ToString(), "Published", run.StartedByUserId,
                    $"Bronze v{reserved.Version} ({run.SyncScope}, {reserved.Mode}): {mtSummary}, {swingSummary}.");
                await db.SaveChangesAsync(ct);
            }

            var bronze = await db.BronzeVersions.SingleOrDefaultAsync(v => v.Version == run.BronzeVersion, ct)
                ?? throw new InvalidOperationException("Run has no pinned Bronze version.");
            if (bronze.Status != BronzeVersionStatus.Published || !Directory.Exists(bronze.Path))
            {
                throw new InvalidOperationException($"Pinned Bronze v{bronze.Version} is not available at {bronze.Path}.");
            }

            ctx.BronzeDir = bronze.Path;
            run.ReferenceVersion ??= await db.ReferenceVersions.MaxAsync(v => (int?)v.Version, ct);
            if (run.ReferenceVersion is { } refVersion)
            {
                var reference = await db.ReferenceVersions.SingleAsync(v => v.Version == refVersion, ct);
                if (!Directory.Exists(reference.Path))
                {
                    throw new InvalidOperationException($"Pinned Bluestar reference bs-{refVersion} is not available at {reference.Path}.");
                }

                ctx.ReferenceDir = reference.Path;
            }

            Transition(run, RunStatus.Running);
            run.SilverPath = ctx.SilverPath;
            await db.SaveChangesAsync(ct);

            await RunStepAsync(run, services.GetRequiredService<NormalizeSwingStep>(), ctx, ct);
            await RunAnalysisAsync(run, ctx, ct);

            Transition(run, RunStatus.Completed);
            run.FinishedAt = Now();
            audit.Write(nameof(PipelineRun), run.RunId, "Completed", run.StartedByUserId,
                $"Analysis completed on Bronze v{run.BronzeVersion}, reference {(run.ReferenceVersion is null ? "none" : $"bs-{run.ReferenceVersion}")}, effective {run.EffectiveDate:yyyy-MM-dd}.");
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run {RunId} failed.", run.RunId);
            run.Status = RunStatus.Failed;
            run.FinishedAt = Now();
            run.ErrorMessage = ex.Message;
            if (reserved is { Status: BronzeVersionStatus.Writing })
            {
                reserved.Status = BronzeVersionStatus.Failed;
            }

            if (staging is not null && Directory.Exists(staging))
            {
                TryDelete(staging);
            }

            audit.Write(nameof(PipelineRun), run.RunId, "Failed", run.StartedByUserId, ex.Message);
            await db.SaveChangesAsync(CancellationToken.None);

            if (ex is OperationCanceledException && ct.IsCancellationRequested)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// W1/W2: re-checks the gate on the latest analysis (all-or-nothing), builds Gold, writes the workbooks + manifest,
    /// then records ExportedItem rows and the wave status in one transaction.
    /// </summary>
    private async Task ExportWaveAsync(PipelineRun run, CancellationToken ct)
    {
        Transition(run, RunStatus.Running);
        await db.SaveChangesAsync(ct);
        var waves = services.GetRequiredService<WaveService>();
        var wave = await db.Waves.SingleAsync(w => w.WaveId == run.WaveId, ct);
        var evaluation = await waves.EvaluateAsync(wave, ct);
        if (!evaluation.Gate.CanExport)
        {
            if (wave.Status != WaveStatus.Exported)
            {
                wave.Status = WaveStatus.Blocked;
            }

            throw new InvalidOperationException("Wave gate failed: "
                + string.Join(", ", evaluation.Gate.Checks.Where(c => !c.Passed).Select(c => $"{c.Name} ({c.Count})")) + ".");
        }

        run.SourceAnalysisRunId = evaluation.Analysis.RunId;
        var exportDir = paths.WaveExportDir(wave.WaveId, run.RunId);
        var tops = DtoMapping.TopBoms(wave.TopBomsJson) ?? [];
        var target = new WaveExportTarget
        {
            WaveId = wave.WaveId,
            TopBoms = tops,
            Items = evaluation.Items,
            GoldPath = paths.GoldPath(run.RunId),
            ExportDir = exportDir
        };
        var ctx = new PipelineContext
        {
            RunId = run.RunId,
            EffectiveDate = wave.EffectiveDate,
            BomTypePolicy = BomTypePolicy.FromJson(wave.BomTypePolicyJson),
            Swing = swingOptions.Value.ToInterpretation(),
            SilverPath = evaluation.Analysis.SilverPath!,
            Wave = target
        };

        await RunStepAsync(run, services.GetRequiredService<WaveGoldStep>(), ctx, ct);
        await RunStepAsync(run, services.GetRequiredService<WaveExportStep>(), ctx, ct);

        var bronze = await db.BronzeVersions.AsNoTracking().SingleAsync(v => v.Version == wave.BronzeVersion, ct);
        var manifest = new
        {
            wave = new { wave.WaveId, wave.Name, topBoms = tops, items = evaluation.Items.Count, wave.EffectiveDate },
            exportRunId = run.RunId,
            analysisRunId = evaluation.Analysis.RunId,
            exportedAt = Now(),
            exportedBy = run.StartedByUserId,
            bronzeVersion = wave.BronzeVersion,
            referenceVersion = $"bs-{wave.ReferenceVersion}",
            bomTypePolicy = JsonDocument.Parse(wave.BomTypePolicyJson).RootElement,
            swing = new
            {
                gsar00ExtractedAt = bronze.SwingExtractedAt,
                gsar00Hash = bronze.SwingItemHash,
                gsst00ExtractedAt = bronze.SwingBomExtractedAt,
                gsst00Hash = bronze.SwingBomHash
            },
            migrationTool = new { maxUpdatedAt = bronze.MaxUpdatedAt, mappingRows = bronze.MappingRows, bronze.MtScopeItems },
            rowCounts = target.RowCounts,
            reusedGlobalVariables = target.ReusedGlobalVariables,
            reusedValueLists = target.ReusedValueLists,
            alreadyExportedReferenced = evaluation.Gate.AlreadyExportedReferenced,
            gate = evaluation.Gate.Checks,
            files = target.Files
        };
        await File.WriteAllTextAsync(Path.Combine(exportDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }), ct);

        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var now = Now();
            var exportedElsewhere = (await db.ExportedItems.Where(e => e.WaveId != wave.WaveId && evaluation.Items.Contains(e.ItemId))
                .Select(e => e.ItemId).Distinct().ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
            var existing = await db.ExportedItems.Where(e => e.WaveId == wave.WaveId).ToDictionaryAsync(e => e.ItemId, ct);
            foreach (var item in evaluation.Items)
            {
                if (existing.TryGetValue(item, out var row))
                {
                    row.RunId = run.RunId;
                    row.ExportedAt = now;
                    row.IsReexport = true;
                }
                else
                {
                    db.ExportedItems.Add(new ExportedItem
                    {
                        ItemId = item, WaveId = wave.WaveId, RunId = run.RunId, ExportedAt = now, IsReexport = exportedElsewhere.Contains(item)
                    });
                }
            }

            wave.Status = WaveStatus.Exported;
            wave.LastExportRunId = run.RunId;
            run.GoldPath = exportDir;
            Transition(run, RunStatus.Completed);
            run.FinishedAt = now;
            audit.Write(nameof(Wave), wave.WaveId, "Exported", run.StartedByUserId,
                $"Exported {evaluation.Items.Count} items ({string.Join(", ", target.Files)}) from analysis {evaluation.Analysis.RunId}.");
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }

    /// <summary>Steps 3-12. Without a Bluestar reference they are recorded as skipped (Swing normalization still runs).</summary>
    private async Task RunAnalysisAsync(PipelineRun run, PipelineContext ctx, CancellationToken ct)
    {
        IPipelineStep[] beforeDecisions =
        [
            services.GetRequiredService<ReferenceStep>(), services.GetRequiredService<MappingStep>(),
            services.GetRequiredService<ClassAttributeStep>(), services.GetRequiredService<GlobalVariableStep>(),
            services.GetRequiredService<InheritanceStep>(), services.GetRequiredService<ItemTypeStep>()
        ];
        IPipelineStep[] afterDecisions =
        [
            services.GetRequiredService<FinalModelStep>(), services.GetRequiredService<BomStep>(), services.GetRequiredService<IssuesStep>()
        ];
        var readiness = services.GetRequiredService<ReadinessStep>();

        if (ctx.ReferenceDir is null)
        {
            foreach (var step in beforeDecisions.Concat(afterDecisions).Append(readiness))
            {
                db.PipelineRunSteps.Add(new PipelineRunStep
                {
                    Id = Guid.NewGuid(),
                    RunId = run.RunId,
                    Sequence = ++sequence,
                    Step = step.Name,
                    OutputTables = string.Join(',', step.OutputTables),
                    Status = StepStatus.Skipped,
                    ErrorMessage = "No Bluestar reference uploaded."
                });
            }

            await db.SaveChangesAsync(ct);
            return;
        }

        foreach (var step in beforeDecisions)
        {
            await RunStepAsync(run, step, ctx, ct);
        }

        ctx.TypeDecisions = await analysisState.SyncTypeDecisionsAsync(run.RunId, ctx.SilverPath, ct);
        foreach (var step in afterDecisions)
        {
            await RunStepAsync(run, step, ctx, ct);
        }

        ctx.ExportedItems = await db.ExportedItems.Select(i => i.ItemId).Distinct().ToListAsync(ct);
        await RunStepAsync(run, readiness, ctx, ct);
        await analysisState.SyncIssueGroupsAsync(run.RunId, ctx.SilverPath, ct);
    }

    private async Task RunStepAsync(PipelineRun run, IPipelineStep step, PipelineContext ctx, CancellationToken ct)
    {
        var record = new PipelineRunStep
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Sequence = ++sequence,
            Step = step.Name,
            OutputTables = string.Join(',', step.OutputTables),
            Status = StepStatus.Running
        };
        db.PipelineRunSteps.Add(record);
        await db.SaveChangesAsync(ct);

        var watch = Stopwatch.StartNew();
        var lastSave = TimeSpan.Zero;
        ctx.Progress = async rows =>
        {
            record.RowsOut = rows;
            if (watch.Elapsed - lastSave >= TimeSpan.FromSeconds(3))
            {
                lastSave = watch.Elapsed;
                record.DurationSeconds = watch.Elapsed.TotalSeconds;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        };
        try
        {
            var result = await step.ExecuteAsync(ctx, ct);
            record.Status = StepStatus.Completed;
            record.RowsOut = result.RowsOut;
        }
        catch (Exception ex)
        {
            record.Status = StepStatus.Failed;
            record.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            ctx.Progress = null;
            record.DurationSeconds = watch.Elapsed.TotalSeconds;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private void PublishBronze(BronzeVersion version, BronzeSyncResult sync, BronzeVersion? previous)
    {
        version.Status = BronzeVersionStatus.Published;
        version.Mode = sync.EffectiveMode;
        version.SyncedAt = Now();
        version.MaxUpdatedAt = sync.MaxUpdatedAt;
        version.ChangedItems = sync.ChangedItems;
        version.MappingRows = sync.MappingRows;
        version.MtItems = sync.MtItems;
        version.MtCarriedFromVersion = sync.MtCarriedFromVersion;
        version.SwingCarriedFromVersion = sync.SwingCarriedFromVersion;

        if (sync.SwingCarriedFromVersion is not null && previous is not null)
        {
            // Files are identical copies, so the extract metadata and hashes carry over unchanged.
            version.SwingItems = previous.SwingItems;
            version.SwingExtractedAt = previous.SwingExtractedAt;
            version.SwingItemHash = previous.SwingItemHash;
            version.SwingBomRows = previous.SwingBomRows;
            version.SwingBomExtractedAt = previous.SwingBomExtractedAt;
            version.SwingBomHash = previous.SwingBomHash;
            return;
        }

        version.SwingItems = (int)(sync.SwingItem?.Rows ?? 0);
        version.SwingExtractedAt = sync.SwingItem?.ExtractedAt;
        version.SwingItemHash = sync.SwingItem?.ContentHash;
        version.SwingBomRows = sync.SwingBom?.Rows ?? 0;
        version.SwingBomExtractedAt = sync.SwingBom?.ExtractedAt;
        version.SwingBomHash = sync.SwingBom?.ContentHash;
    }

    private static void Transition(PipelineRun run, RunStatus to)
    {
        if (!RunTransitions.CanTransition(run.Status, to))
        {
            throw new InvalidOperationException($"Run {run.RunId} cannot move from {run.Status} to {to}.");
        }

        run.Status = to;
    }

    private void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Could not delete staging folder {Dir}.", dir);
        }
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
