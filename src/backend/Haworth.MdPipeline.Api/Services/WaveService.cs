using System.Text.Json;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Api.Jobs;
using Haworth.MdPipeline.Core.Domain;
using Haworth.MdPipeline.Core.Domain.Metadata;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Data;
using Haworth.MdPipeline.Pipeline.Duck;
using Microsoft.EntityFrameworkCore;

namespace Haworth.MdPipeline.Api.Services;

/// <summary>
/// BOM waves: a named set of top BOMs pinned to the Bronze snapshot and Bluestar reference of the analysis it was built on.
/// The gate is evaluated on the latest completed analysis; export is queued as a WaveExport run.
/// </summary>
public sealed class WaveService(
    MdPipelineDbContext db,
    AuditService audit,
    AnalysisQueryService analysis,
    RunQueue queue,
    DataPaths paths,
    TimeProvider clock)
{
    public const int MaxTopBoms = 1000;

    public sealed record GateEvaluation(WaveGateDto Gate, PipelineRun Analysis, IReadOnlyList<string> Items);

    public async Task<WaveDto> CreateAsync(CreateWaveRequestDto request, string actorId, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 200)
        {
            throw new DomainValidationException("Wave name is required (max 200 characters).");
        }

        var tops = NormalizeTops(request.TopBoms);
        var (run, duck) = await analysis.OpenAsync(ct);
        int items;
        using (duck)
        {
            items = CheckTops(duck, tops, run).Count;
        }

        var number = await db.Waves.CountAsync(ct) + 1;
        string waveId;
        do
        {
            waveId = $"W-{number++:00}";
        }
        while (await db.Waves.AnyAsync(w => w.WaveId == waveId, ct));

        var wave = new Wave
        {
            WaveId = waveId,
            Name = name,
            TopBomsJson = JsonSerializer.Serialize(tops),
            EffectiveDate = request.EffectiveDate ?? run.EffectiveDate,
            BomTypePolicyJson = run.BomTypePolicyJson,
            Items = items,
            Status = WaveStatus.Draft,
            BronzeVersion = run.BronzeVersion ?? throw new ConflictException($"Analysis {run.RunId} has no Bronze version."),
            ReferenceVersion = run.ReferenceVersion ?? throw new ConflictException($"Analysis {run.RunId} has no Bluestar reference."),
            CreatedByUserId = actorId,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.Waves.Add(wave);
        audit.Write(nameof(Wave), waveId, "Created", actorId,
            $"Wave '{name}' with {tops.Count} top BOM(s), {items} items on Bronze v{wave.BronzeVersion}, bs-{wave.ReferenceVersion} (analysis {run.RunId}).");
        await db.SaveChangesAsync(ct);
        return wave.ToDto();
    }

    public async Task<PagedResult<WaveDto>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        (limit, offset) = RunService.Page(limit, offset);
        var total = await db.Waves.CountAsync(ct);
        var waves = await db.Waves.AsNoTracking().OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.WaveId)
            .Skip(offset).Take(limit).ToListAsync(ct);
        return new PagedResult<WaveDto>(waves.Select(w => w.ToDto()).ToList(), total, limit, offset);
    }

    public async Task<WaveDto> GetAsync(string waveId, CancellationToken ct) => (await FindAsync(waveId, ct)).ToDto();

    /// <summary>Re-pins the wave to the latest analysis (new Bronze snapshot / reference) and recounts its items.</summary>
    public async Task<WaveDto> RefreshAsync(string waveId, string actorId, CancellationToken ct)
    {
        var wave = await FindAsync(waveId, ct);
        var tops = DtoMapping.TopBoms(wave.TopBomsJson) ?? [];
        var (run, duck) = await analysis.OpenAsync(ct);
        using (duck)
        {
            wave.Items = CheckTops(duck, tops, run).Count;
        }

        var before = $"Bronze v{wave.BronzeVersion}, bs-{wave.ReferenceVersion}";
        wave.BronzeVersion = run.BronzeVersion!.Value;
        wave.ReferenceVersion = run.ReferenceVersion ?? throw new ConflictException($"Analysis {run.RunId} has no Bluestar reference.");
        wave.BomTypePolicyJson = run.BomTypePolicyJson;
        if (wave.Status != WaveStatus.Exported)
        {
            wave.Status = WaveStatus.Draft;
        }

        audit.Write(nameof(Wave), wave.WaveId, "Refreshed", actorId,
            $"Re-pinned from {before} to Bronze v{wave.BronzeVersion}, bs-{wave.ReferenceVersion} (analysis {run.RunId}); {wave.Items} items.");
        await db.SaveChangesAsync(ct);
        return wave.ToDto();
    }

    public async Task<WaveGateDto> GateAsync(string waveId, CancellationToken ct)
    {
        var wave = await FindAsync(waveId, ct);
        var evaluation = await EvaluateAsync(wave, ct);
        if (wave.Status != WaveStatus.Exported)
        {
            wave.Status = evaluation.Gate.CanExport ? WaveStatus.Ready : WaveStatus.Blocked;
            await db.SaveChangesAsync(ct);
        }

        return evaluation.Gate;
    }

    public async Task<GateEvaluation> EvaluateAsync(Wave wave, CancellationToken ct)
    {
        var tops = DtoMapping.TopBoms(wave.TopBomsJson) ?? [];
        var (run, duck) = await analysis.OpenAsync(ct);
        Dictionary<string, object?> counts;
        List<string> items;
        List<Dictionary<string, object?>> typeChanges;
        var joined = string.Join('\u001f', tops);
        using (duck)
        {
            counts = duck.Query(SqlScriptLoader.Load("q_wave_gate.sql"), joined)[0];
            items = duck.Query(SqlScriptLoader.Load("q_wave_items.sql"), joined).Select(r => (string)r["item_id"]!).ToList();
            typeChanges = duck.Query(SqlScriptLoader.Load("q_wave_type_changes.sql"), joined);
        }

        var changedIds = typeChanges.Select(r => (string)r["item_id"]!).ToList();
        var decisions = await db.TypeDecisions.AsNoTracking().Where(d => changedIds.Contains(d.ItemId)).ToDictionaryAsync(d => d.ItemId, ct);
        var notApproved = typeChanges.Count(r =>
            !decisions.TryGetValue((string)r["item_id"]!, out var d)
            || d.Decision != TypeDecisionStatus.Approved
            || d.InheritedAttrsAtDecision != ((string?)r["inherited_attrs"] ?? string.Empty)
            || d.SwingTypeCodeAtDecision != (string?)r["swing_type_code"]);

        var latestBronze = await db.BronzeVersions.Where(v => v.Status == BronzeVersionStatus.Published).MaxAsync(v => (int?)v.Version, ct);
        var latestReference = await db.ReferenceVersions.MaxAsync(v => (int?)v.Version, ct);
        var sourceChanges =
            (run.BronzeVersion != wave.BronzeVersion ? 1 : 0)
            + (run.ReferenceVersion != wave.ReferenceVersion ? 1 : 0)
            + (latestBronze > wave.BronzeVersion ? 1 : 0)
            + (latestReference > wave.ReferenceVersion ? 1 : 0)
            + Convert.ToInt32(counts["unknown_tops"]);

        var checks = WaveGate.Evaluate(new WaveGateInputs(
            Convert.ToInt32(counts["issue_items"]), Convert.ToInt32(counts["not_imported"]), notApproved,
            Convert.ToInt32(counts["unknown_types"]), Convert.ToInt32(counts["missing_references"]), sourceChanges));

        var exportedElsewhere = await db.ExportedItems.AsNoTracking()
            .Where(e => e.WaveId != wave.WaveId && items.Contains(e.ItemId))
            .Select(e => e.ItemId).Distinct().CountAsync(ct);

        return new GateEvaluation(
            new WaveGateDto(wave.WaveId, items.Count, exportedElsewhere, checks, WaveGate.CanExport(checks), run.RunId, latestBronze, latestReference),
            run, items);
    }

    /// <summary>409 when the gate fails or an export of this wave is already queued/running.</summary>
    public async Task<RunDto> QueueExportAsync(string waveId, string actorId, CancellationToken ct)
    {
        var wave = await FindAsync(waveId, ct);
        if (await db.PipelineRuns.AnyAsync(r => r.WaveId == waveId
                && (r.Status == RunStatus.Queued || r.Status == RunStatus.Syncing || r.Status == RunStatus.Running), ct))
        {
            throw new ConflictException($"An export of {waveId} is already queued or running.");
        }

        var evaluation = await EvaluateAsync(wave, ct);
        if (!evaluation.Gate.CanExport)
        {
            if (wave.Status != WaveStatus.Exported)
            {
                wave.Status = WaveStatus.Blocked;
                await db.SaveChangesAsync(ct);
            }

            var failed = evaluation.Gate.Checks.Where(c => !c.Passed).Select(c => $"{c.Name} ({c.Count})");
            throw new ConflictException($"Wave gate failed: {string.Join(", ", failed)}.");
        }

        var now = clock.GetUtcNow();
        var run = new PipelineRun
        {
            RunId = $"E-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}",
            RunType = RunType.WaveExport,
            WaveId = wave.WaveId,
            SourceAnalysisRunId = evaluation.Analysis.RunId,
            BronzeVersion = wave.BronzeVersion,
            ReferenceVersion = wave.ReferenceVersion,
            EffectiveDate = wave.EffectiveDate,
            BomTypePolicyJson = wave.BomTypePolicyJson,
            Status = RunStatus.Queued,
            StartedByUserId = actorId,
            QueuedAt = now.UtcDateTime
        };
        db.PipelineRuns.Add(run);
        audit.Write(nameof(PipelineRun), run.RunId, "Queued", actorId, $"Export of wave {wave.WaveId} ({evaluation.Items.Count} items) queued.");
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(run.RunId, ct);
        return run.ToDto();
    }

    public async Task<IReadOnlyList<WaveFileDto>> FilesAsync(string waveId, CancellationToken ct)
    {
        var wave = await FindAsync(waveId, ct);
        if (wave.LastExportRunId is null)
        {
            return [];
        }

        var dir = paths.WaveExportDir(wave.WaveId, wave.LastExportRunId);
        return Directory.Exists(dir)
            ? Directory.GetFiles(dir).Select(f => new FileInfo(f)).OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => new WaveFileDto(f.Name, f.Length, wave.LastExportRunId)).ToList()
            : [];
    }

    /// <summary>Resolves a file of the last export; the name must be one of the listed files (no path traversal).</summary>
    public async Task<string> FilePathAsync(string waveId, string file, CancellationToken ct)
    {
        var files = await FilesAsync(waveId, ct);
        var match = files.SingleOrDefault(f => string.Equals(f.Name, file, StringComparison.Ordinal))
            ?? throw new NotFoundException($"File '{file}' was not found for wave {waveId}.");
        return Path.Combine(paths.WaveExportDir(waveId, match.RunId), match.Name);
    }

    private async Task<Wave> FindAsync(string waveId, CancellationToken ct) =>
        await db.Waves.SingleOrDefaultAsync(w => w.WaveId == waveId, ct) ?? throw new NotFoundException($"Wave '{waveId}' was not found.");

    private static List<string> NormalizeTops(IReadOnlyList<string>? topBoms)
    {
        var tops = (topBoms ?? []).Select(b => b?.Trim() ?? string.Empty).Where(b => b.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (tops.Count == 0)
        {
            throw new DomainValidationException("Select at least one top BOM.");
        }

        if (tops.Count > MaxTopBoms)
        {
            throw new DomainValidationException($"Select at most {MaxTopBoms} top BOMs per wave.");
        }

        return tops;
    }

    private static List<string> CheckTops(DuckDbExecutor duck, IReadOnlyList<string> tops, PipelineRun run)
    {
        var joined = string.Join('\u001f', tops);
        var known = duck.Query("SELECT top_item_id FROM top_bom WHERE top_item_id IN (SELECT unnest(string_split($1, chr(31))))", joined)
            .Select(r => (string)r["top_item_id"]!).ToHashSet(StringComparer.Ordinal);
        var unknown = tops.Where(t => !known.Contains(t)).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainValidationException($"Not a top BOM in analysis {run.RunId}: {string.Join(", ", unknown.Take(20))}.");
        }

        return duck.Query(SqlScriptLoader.Load("q_wave_items.sql"), joined).Select(r => (string)r["item_id"]!).ToList();
    }
}
