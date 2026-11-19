using System.Runtime.CompilerServices;
using System.Text.Json;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>
/// Used when MigrationTool:UseFixtures=true. Reads {FixturesDir}/mt/mappings.json and items.json in the live API shape
/// and parses them with the same <see cref="MtPayload"/> rules as the REST client.
/// </summary>
public sealed class FixtureMigrationToolClient(IOptions<PipelineOptions> options) : IMigrationToolClient
{
    private readonly string dir = Path.Combine(options.Value.FixturesDir, "mt");

    public bool SupportsBulkMappings => true;

    public async IAsyncEnumerable<MtMappingRow> GetMappingsBulkAsync(DateTimeOffset? updatedSince, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var row in await LoadMappingsAsync(ct))
        {
            if (updatedSince is null || row.UpdatedAt is null || row.UpdatedAt > updatedSince)
            {
                yield return row;
            }
        }
    }

    public async Task<IReadOnlyList<MtMappingRow>> GetItemMappingsAsync(string itemId, CancellationToken ct) =>
        (await LoadMappingsAsync(ct)).Where(r => r.LegacyItemId == itemId).ToList();

    public async Task<IReadOnlyList<MtItemHeader>> GetItemsByIdsAsync(IReadOnlyList<string> itemIds, CancellationToken ct)
    {
        var wanted = itemIds.ToHashSet(StringComparer.Ordinal);
        using var doc = await LoadAsync("items.json", ct);
        return MtPayload.Rows(doc.RootElement).Select(MtPayload.Item).Where(i => wanted.Contains(i.ItemId)).ToList();
    }

    public async Task<IReadOnlyList<MtItemHeader>> GetAllItemsAsync(Func<long, Task>? progress, CancellationToken ct)
    {
        using var doc = await LoadAsync("items.json", ct);
        return MtPayload.Rows(doc.RootElement).Select(MtPayload.Item).ToList();
    }

    public Task<SourceHealth> CheckAsync(CancellationToken ct)
    {
        var ok = File.Exists(Path.Combine(dir, "mappings.json")) && File.Exists(Path.Combine(dir, "items.json"));
        return Task.FromResult(new SourceHealth("MigrationTool", ok, ok ? $"Fixture mode ({Path.GetFullPath(dir)})." : $"Fixture files missing in {Path.GetFullPath(dir)}."));
    }

    private async Task<List<MtMappingRow>> LoadMappingsAsync(CancellationToken ct)
    {
        using var doc = await LoadAsync("mappings.json", ct);
        return MtPayload.Rows(doc.RootElement).Select(r => MtPayload.Mapping(r)).ToList();
    }

    private async Task<JsonDocument> LoadAsync(string file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(Path.Combine(dir, file));
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
