using System.Runtime.CompilerServices;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Core.Rules;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Steps;

/// <summary>
/// Step 1: Migration Tool → Bronze mappings (bucketed) and item headers. The item set comes from the Migration Tool itself
/// (GET /bom/items); a BOM-scoped import narrows it to the selected BOMs' GSST00 explosions, so it runs after the Swing extracts.
/// </summary>
public sealed class SyncMigrationToolStep(
    IMigrationToolClient client,
    BronzeParquetWriter writer,
    IOptions<PipelineOptions> pipelineOptions,
    IOptions<MigrationToolOptions> mtOptions) : IPipelineStep
{
    private static readonly BronzeTableSpec MappingSpec = new("mappings",
    [
        new("legacy_item_id"), new("legacy_feature_id"), new("legacy_value"), new("new_attribute_id"), new("new_value"),
        new("attribute_type"), new("condition"), new("simplified_legacy_condition"), new("target_condition"), new("formula"),
        new("mapped_from"), new("value_status"), new("feasibility"), new("legacy_reachable", "BOOLEAN"),
        new("inherited"), new("prediction_status"), new("prediction_confidence"), new("prediction_match_type"),
        new("prediction_evidence"), new("signed_on_by"), new("signed_on_at", "TIMESTAMP"), new("updated_at", "TIMESTAMP"),
        new("raw_json"), new("item_bucket")
    ]);

    private static readonly BronzeTableSpec ItemSpec = new("items",
    [
        new("item_id"), new("description"), new("category"), new("product_type"), new("priority"),
        new("approved_for_migration", "BOOLEAN"), new("raw_json")
    ]);

    public string Name => nameof(SyncMigrationToolStep);
    public IReadOnlyList<string> OutputTables => ["mappings", "items"];

    public async Task<StepResult> ExecuteAsync(PipelineContext ctx, CancellationToken ct)
    {
        var target = ctx.BronzeTarget ?? throw new InvalidOperationException("Sync step requires a Bronze target.");
        if (ctx.MtScopeTopBoms is { Count: > 0 } topBoms)
        {
            return await ExecuteScopedAsync(ctx, target, topBoms, ct);
        }

        var options = pipelineOptions.Value;
        var staging = target.StagingDir;
        var literals = new Dictionary<string, string> { ["bronze_dir"] = SqlScriptLoader.SqlPath(staging) };

        using var duck = DuckDbExecutor.Open(null, options);

        // Only items loaded in the Migration Tool are fetched (not every Swing item); the list also carries the item headers.
        var mtItems = await client.GetAllItemsAsync(ctx.Progress, ct);
        var mtIds = mtItems.Select(i => i.ItemId).Order(StringComparer.Ordinal).ToList();

        var incremental = target.Mode == SyncMode.Incremental
                          && client.SupportsBulkMappings
                          && HasMappingFiles(target.PreviousDir);
        ctx.Sync.EffectiveMode = incremental ? SyncMode.Incremental : SyncMode.Full;

        var source = client.SupportsBulkMappings
            ? client.GetMappingsBulkAsync(incremental ? target.PreviousMaxUpdatedAt : null, ct)
            : PerItemAsync(mtIds, ctx, ct);
        var changed = new HashSet<string>(StringComparer.Ordinal);
        var meta = new BronzeMeta(target.Version, DateTime.UtcNow, "MigrationTool", "workspace-mappings");
        var rows = ToRows(source, changed, options.MappingBuckets, ct);

        if (incremental)
        {
            var deltaPath = Path.Combine(staging, "_mappings_delta.parquet");
            await writer.WriteAsync(MappingSpec, rows, meta, deltaPath, partitionBy: null, ct);
            duck.ExecuteFile("01_mappings_merge.sql", new Dictionary<string, string>(literals)
            {
                ["previous_dir"] = SqlScriptLoader.SqlPath(target.PreviousDir!),
                ["delta_path"] = SqlScriptLoader.SqlPath(deltaPath)
            });
            File.Delete(deltaPath);
        }
        else
        {
            await writer.WriteAsync(MappingSpec, rows, meta, Path.Combine(staging, "mappings"), partitionBy: "item_bucket", ct);
        }

        ctx.Sync.ChangedItems = changed.Count;
        var mappingItemIds = new List<string>();
        if (HasMappingFiles(staging))
        {
            var summary = duck.Query(SqlScriptLoader.Load("01_mappings_summary.sql", literals)).Single();
            ctx.Sync.MappingRows = Convert.ToInt64(summary["mapping_rows"]);
            ctx.Sync.MaxUpdatedAt = summary["max_updated_at"] as DateTime?;
            mappingItemIds = duck.Query(SqlScriptLoader.Load("01_mapping_item_ids.sql", literals))
                .Select(r => (string)r["item_id"]!)
                .ToList();
        }

        // Bulk mappings may name items missing from /bom/items: look those headers up by id (normally none).
        var listed = mtIds.ToHashSet(StringComparer.Ordinal);
        var headers = new List<MtItemHeader>(mtItems);
        foreach (var batch in mappingItemIds.Where(id => !listed.Contains(id)).Chunk(Math.Max(1, mtOptions.Value.ItemsBatchSize)))
        {
            headers.AddRange(await client.GetItemsByIdsAsync(batch, ct));
        }

        var itemResult = await writer.WriteAsync(
            ItemSpec,
            ToAsync(headers.Select(h => new object?[] { h.ItemId, h.Description, h.Category, h.ProductType, h.Priority, h.ApprovedForMigration, h.RawJson })),
            meta with { SourceTable = "bom/items" },
            Path.Combine(staging, "items.parquet"),
            partitionBy: null,
            ct);
        ctx.Sync.MtItems = (int)itemResult.Rows;

        return new StepResult(ctx.Sync.MappingRows + itemResult.Rows);
    }

    /// <summary>
    /// BOM-scoped import: fetches mappings and headers only for the selected BOMs' explosions (per-item calls) and keeps the
    /// previous snapshot's Migration Tool data for every other item.
    /// </summary>
    private async Task<StepResult> ExecuteScopedAsync(PipelineContext ctx, BronzeTarget target, IReadOnlyList<string> topBoms, CancellationToken ct)
    {
        var options = pipelineOptions.Value;
        var staging = target.StagingDir;
        var literals = new Dictionary<string, string> { ["bronze_dir"] = SqlScriptLoader.SqlPath(staging) };
        using var duck = DuckDbExecutor.Open(null, options);

        var scopeItems = duck.Query(SqlScriptLoader.Load("01_scope_bom_explosion.sql", literals), string.Join('\u001f', topBoms))
            .Select(r => (string)r["item_id"]!)
            .ToList();
        ctx.Sync.EffectiveMode = SyncMode.Full;
        ctx.Sync.MtScopeItems = scopeItems.Count;
        duck.ExecuteFile("01_scope_item_table.sql");
        duck.Append("scope_item", scopeItems.Select(id => new object?[] { id }));

        // The whole explosion is replaced, but only its items loaded in the Migration Tool are requested
        // (purchased parts etc. that the Migration Tool does not know would only return empty results).
        var scopeSet = scopeItems.ToHashSet(StringComparer.Ordinal);
        var headers = (await client.GetAllItemsAsync(ctx.Progress, ct)).Where(h => scopeSet.Contains(h.ItemId)).ToList();
        var fetchIds = headers.Select(h => h.ItemId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var meta = new BronzeMeta(target.Version, DateTime.UtcNow, "MigrationTool", "workspace-mappings");
        var changed = new HashSet<string>(StringComparer.Ordinal);
        var mappingDelta = Path.Combine(staging, "_mappings_delta.parquet");
        await writer.WriteAsync(MappingSpec, ToRows(PerItemAsync(fetchIds, ctx, ct), changed, options.MappingBuckets, ct), meta, mappingDelta, partitionBy: null, ct);
        ctx.Sync.ChangedItems = changed.Count;

        var itemDelta = Path.Combine(staging, "_items_delta.parquet");
        await writer.WriteAsync(
            ItemSpec,
            ToAsync(headers.Select(h => new object?[] { h.ItemId, h.Description, h.Category, h.ProductType, h.Priority, h.ApprovedForMigration, h.RawJson })),
            meta with { SourceTable = "bom/items" },
            itemDelta,
            partitionBy: null,
            ct);

        var merge = new Dictionary<string, string>(literals)
        {
            ["previous_dir"] = SqlScriptLoader.SqlPath(target.PreviousDir ?? staging),
            ["delta_path"] = SqlScriptLoader.SqlPath(mappingDelta)
        };
        duck.ExecuteFile(HasMappingFiles(target.PreviousDir) ? "01_mappings_merge_scoped.sql" : "01_mappings_from_delta.sql", merge);
        File.Delete(mappingDelta);

        merge["delta_path"] = SqlScriptLoader.SqlPath(itemDelta);
        if (target.PreviousDir is not null && File.Exists(Path.Combine(target.PreviousDir, "items.parquet")))
        {
            duck.ExecuteFile("01_items_merge_scoped.sql", merge);
            File.Delete(itemDelta);
        }
        else
        {
            File.Move(itemDelta, Path.Combine(staging, "items.parquet"));
        }

        if (HasMappingFiles(staging))
        {
            var summary = duck.Query(SqlScriptLoader.Load("01_mappings_summary.sql", literals)).Single();
            ctx.Sync.MappingRows = Convert.ToInt64(summary["mapping_rows"]);
            ctx.Sync.MaxUpdatedAt = summary["max_updated_at"] as DateTime?;
        }

        ctx.Sync.MtItems = Convert.ToInt32(duck.Query(SqlScriptLoader.Load("01_items_count.sql", literals)).Single()["item_rows"]);
        return new StepResult(ctx.Sync.MappingRows + ctx.Sync.MtItems);
    }

    private async IAsyncEnumerable<MtMappingRow> PerItemAsync(IReadOnlyList<string> itemIds, PipelineContext ctx, [EnumeratorCancellation] CancellationToken ct)
    {
        var window = Math.Max(1, mtOptions.Value.MaxConcurrency) * 4;
        long done = 0;
        foreach (var chunk in itemIds.Chunk(window))
        {
            var results = await Task.WhenAll(chunk.Select(id => client.GetItemMappingsAsync(id, ct)));
            foreach (var row in results.SelectMany(r => r))
            {
                yield return row;
            }

            done += chunk.Length;
            if (ctx.Progress is { } progress)
            {
                await progress(done);
            }
        }
    }

    private static async IAsyncEnumerable<object?[]> ToRows(
        IAsyncEnumerable<MtMappingRow> rows, HashSet<string> changed, int buckets, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var r in rows.WithCancellation(ct))
        {
            changed.Add(r.LegacyItemId);
            yield return
            [
                r.LegacyItemId, r.LegacyFeatureId, r.LegacyValue, r.NewAttributeId, r.NewValue,
                r.AttributeType, r.Condition, r.SimplifiedLegacyCondition, r.TargetCondition, r.Formula,
                r.MappedFrom, r.ValueStatus, r.Feasibility, r.LegacyReachable,
                r.Inherited, r.PredictionStatus, r.PredictionConfidence, r.PredictionMatchType,
                r.PredictionEvidence, r.SignedOnBy, r.SignedOnAt?.UtcDateTime, r.UpdatedAt?.UtcDateTime,
                r.RawJson, ItemIds.Bucket(r.LegacyItemId, buckets)
            ];
        }
    }

    private static bool HasMappingFiles(string? bronzeDir) =>
        bronzeDir is not null
        && Directory.Exists(Path.Combine(bronzeDir, "mappings"))
        && Directory.EnumerateFiles(Path.Combine(bronzeDir, "mappings"), "*.parquet", SearchOption.AllDirectories).Any();

#pragma warning disable CS1998
    private static async IAsyncEnumerable<object?[]> ToAsync(IEnumerable<object?[]> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
    }
#pragma warning restore CS1998
}
