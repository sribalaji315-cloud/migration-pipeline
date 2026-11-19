using System.Net;
using System.Text;
using System.Text.Json;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Duck;
using Haworth.MdPipeline.Pipeline.Options;
using Haworth.MdPipeline.Pipeline.Sources;
using Haworth.MdPipeline.Pipeline.Steps;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Haworth.MdPipeline.Pipeline.Tests;

public sealed class SyncMigrationToolStepTests : IDisposable
{
    private readonly TestWorkspace ws = new();

    public void Dispose() => ws.Dispose();

    private SyncMigrationToolStep Step() => new(
        new FixtureMigrationToolClient(MsOptions.Create(ws.Options)),
        ws.Writer,
        MsOptions.Create(ws.Options),
        MsOptions.Create(new MigrationToolOptions()));

    private long CountMappings(string bronzeDir)
    {
        using var duck = DuckDbExecutor.Open(null, ws.Options);
        return Convert.ToInt64(duck.Query(SqlScriptLoader.Load("01_mappings_summary.sql",
            new Dictionary<string, string> { ["bronze_dir"] = SqlScriptLoader.SqlPath(bronzeDir) })).Single()["mapping_rows"]);
    }

    [Fact]
    public async Task FullSyncWritesBucketedMappingsAndScopedItemHeaders()
    {
        var bronze = await ws.SyncSwingAsync();
        var ctx = ws.Context(bronze, target: new BronzeTarget(1, bronze, SyncMode.Full, null, null));

        await Step().ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(SyncMode.Full, ctx.Sync.EffectiveMode);
        Assert.Equal(3, ctx.Sync.MappingRows);
        Assert.Equal(2, ctx.Sync.ChangedItems);
        Assert.Equal(3, ctx.Sync.MtItems);
        Assert.Equal(new DateTime(2026, 11, 3, 10, 0, 0), ctx.Sync.MaxUpdatedAt);
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(bronze, "mappings"), "item_bucket=*"));
    }

    [Fact]
    public async Task IncrementalSyncReplacesOnlyChangedItems()
    {
        var v1 = await ws.SyncSwingAsync(1);
        await Step().ExecuteAsync(ws.Context(v1, target: new BronzeTarget(1, v1, SyncMode.Full, null, null)), CancellationToken.None);

        var v2 = await ws.SyncSwingAsync(2);
        var ctx = ws.Context(v2, target: new BronzeTarget(2, v2, SyncMode.Incremental, v1, new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero)));
        await Step().ExecuteAsync(ctx, CancellationToken.None);

        Assert.Equal(SyncMode.Incremental, ctx.Sync.EffectiveMode);
        Assert.Equal(1, ctx.Sync.ChangedItems);
        Assert.Equal(3, ctx.Sync.MappingRows);
        Assert.Equal(3, CountMappings(v1));
    }

    [Theory]
    [InlineData("GET", "/export/workspace-mappings", true)]
    [InlineData("POST", "/auth/login", true)]
    [InlineData("POST", "/auth/refresh", true)]
    [InlineData("POST", "/bom/items/by-ids", true)]
    [InlineData("POST", "/workspace-mappings/A1", false)]
    [InlineData("PUT", "/workspace-mappings/A1", false)]
    [InlineData("DELETE", "/workspace-mappings/A1", false)]
    [InlineData("PATCH", "/bom/items/by-ids", false)]
    public void GuardAllowsOnlyReadOperations(string method, string path, bool allowed) =>
        Assert.Equal(allowed, ReadOnlyGuardHandler.IsAllowed(new HttpMethod(method), path));

    [Fact]
    public async Task ClientIssuesOnlyReadOnlyRequests()
    {
        var recorder = new RecordingHandler();
        var options = MsOptions.Create(new MigrationToolOptions { BulkPageSize = 2, User = "admin", Password = "p&w", BearerToken = "ignored-static-token" });
        var http = new HttpClient(new ReadOnlyGuardHandler(options) { InnerHandler = recorder }) { BaseAddress = new Uri("http://mt.test") };
        var client = new MigrationToolClient(http, options);

        var rows = new List<MtMappingRow>();
        await foreach (var row in client.GetMappingsBulkAsync(null, CancellationToken.None))
        {
            rows.Add(row);
        }

        await client.GetItemsByIdsAsync(["A5TBL01"], CancellationToken.None);
        await client.GetItemMappingsAsync("A5TBL01", CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.All(recorder.Requests, r => Assert.True(ReadOnlyGuardHandler.IsAllowed(r.Method, r.Path), $"{r.Method} {r.Path}"));
        Assert.Contains(recorder.Requests, r => r.Path == "/auth/login");
        // Login is a form post (JSON gets 422), the snake_case access_token is used, and user/password win over a static token.
        Assert.Equal("username=admin&password=p%26w", recorder.Bodies[0]);
        Assert.All(recorder.Requests.Where(r => r.Path != "/auth/login"), r => Assert.Equal("Bearer t", r.Authorization));
    }

    [Fact]
    public async Task ClientRefusesWriteVerbsBeforeSending()
    {
        var recorder = new RecordingHandler();
        var http = new HttpClient(new ReadOnlyGuardHandler(MsOptions.Create(new MigrationToolOptions())) { InnerHandler = recorder })
        {
            BaseAddress = new Uri("http://mt.test")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => http.DeleteAsync("/workspace-mappings/A1"));
        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public void ParsesLiveMappingShape()
    {
        using var doc = JsonDocument.Parse("""
            [{"itemId":"ASYCB3S0","legacyFeatureId":"2BV","legacyValue":"0","newAttributeId":"LevelCombination","newValue":"FF",
              "attributeType":"engineering","condition":null,"formula":null,"mappedFrom":"predicted","valueStatus":"discontinued",
              "feasibility":"Review","inherited":"No","signedOnByUserId":"USR-2","signedOnByUsername":"admin",
              "signedOnAt":1793775605.726441,"updatedAt":1793775735.074464,"allGlobalTargets":["LevelCombination"],
              "candidateAttributeIds":null,
              "prediction":{"status":"MAPPED","confidence":"High","matchType":"GLOBAL","evidence":"candidates","predictedAt":1793775600.0}}]
            """);

        var row = MtPayload.Mapping(Assert.Single(MtPayload.Rows(doc.RootElement)));

        Assert.Equal(("ASYCB3S0", "No", "High", "GLOBAL", "admin"), (row.LegacyItemId, row.Inherited, row.PredictionConfidence, row.PredictionMatchType, row.SignedOnBy));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793775735074), row.UpdatedAt!.Value.AddTicks(-(row.UpdatedAt.Value.Ticks % TimeSpan.TicksPerMillisecond)));
        Assert.Null(row.LegacyReachable);
        Assert.Contains("\"allGlobalTargets\"", row.RawJson);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("\"Yes\"", true)]
    [InlineData("\"no\"", false)]
    [InlineData("\"maybe\"", null)]
    [InlineData("null", null)]
    public void ParsesFlagsWithoutDefaultingUnknownValues(string json, bool? expected)
    {
        using var doc = JsonDocument.Parse($$"""{"approvedForMigration":{{json}},"itemId":"X"}""");

        Assert.Equal(expected, MtPayload.Item(doc.RootElement).ApprovedForMigration);
    }

    [Fact]
    public async Task PerItemSyncFetchesOnlyItemsListedInTheMigrationTool()
    {
        var bronze = await ws.SyncSwingAsync();
        var client = new PerItemClient(["MT1", "MT2"]);
        var step = new SyncMigrationToolStep(client, ws.Writer, MsOptions.Create(ws.Options), MsOptions.Create(new MigrationToolOptions()));
        var ctx = ws.Context(bronze, target: new BronzeTarget(1, bronze, SyncMode.Full, null, null));

        await step.ExecuteAsync(ctx, CancellationToken.None);

        // GSST00 items (A5TBL01, …) are not requested: only the Migration Tool's own item list.
        Assert.Equal(["MT1", "MT2"], client.Requested.Order(StringComparer.Ordinal));
        Assert.Equal(2, ctx.Sync.MtItems);
        Assert.Empty(client.HeaderLookups);
    }

    [Fact]
    public async Task ItemListIsPagedUntilAShortPage()
    {
        var recorder = new RecordingHandler();
        var options = MsOptions.Create(new MigrationToolOptions { BearerToken = "t", ItemsPageSize = 2 });
        var client = new MigrationToolClient(new HttpClient(new ReadOnlyGuardHandler(options) { InnerHandler = recorder }) { BaseAddress = new Uri("http://mt.test") }, options);

        var items = await client.GetAllItemsAsync(null, CancellationToken.None);

        Assert.Equal(["I1", "I2", "I3"], items.Select(i => i.ItemId));
        Assert.Equal(2, recorder.Requests.Count(r => r.Path == "/bom/items"));
    }

    private sealed class PerItemClient(IReadOnlyList<string> listed) : IMigrationToolClient
    {
        public System.Collections.Concurrent.ConcurrentBag<string> Requested { get; } = [];
        public List<string> HeaderLookups { get; } = [];
        public bool SupportsBulkMappings => false;

        public IAsyncEnumerable<MtMappingRow> GetMappingsBulkAsync(DateTimeOffset? updatedSince, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<MtMappingRow>> GetItemMappingsAsync(string itemId, CancellationToken ct)
        {
            Requested.Add(itemId);
            using var doc = JsonDocument.Parse($$"""[{"itemId":"{{itemId}}","legacyFeatureId":"F","legacyValue":"1","newAttributeId":"A","newValue":"V"}]""");
            return Task.FromResult<IReadOnlyList<MtMappingRow>>(MtPayload.Rows(doc.RootElement).Select(r => MtPayload.Mapping(r)).ToList());
        }

        public Task<IReadOnlyList<MtItemHeader>> GetItemsByIdsAsync(IReadOnlyList<string> itemIds, CancellationToken ct)
        {
            HeaderLookups.AddRange(itemIds);
            return Task.FromResult<IReadOnlyList<MtItemHeader>>([]);
        }

        public Task<IReadOnlyList<MtItemHeader>> GetAllItemsAsync(Func<long, Task>? progress, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse("[" + string.Join(',', listed.Select(id => $$"""{"itemId":"{{id}}","approvedForMigration":true}""")) + "]");
            return Task.FromResult<IReadOnlyList<MtItemHeader>>(MtPayload.Rows(doc.RootElement).Select(MtPayload.Item).ToList());
        }

        public Task<SourceHealth> CheckAsync(CancellationToken ct) => Task.FromResult(new SourceHealth("MigrationTool", true, "fake"));
    }

    [Fact]
    public async Task ItemHeadersAreRequestedWithABareIdArray()
    {
        var recorder = new RecordingHandler();
        var client = TokenClient(recorder, Jwt(DateTimeOffset.UtcNow.AddHours(1)));

        var items = await client.GetItemsByIdsAsync(["A5TBL01", "00123"], CancellationToken.None);

        Assert.Equal("""["A5TBL01","00123"]""", recorder.Bodies.Single());
        Assert.Equal("A5TBL01", Assert.Single(items).ItemId);
    }

    [Fact]
    public async Task BearerTokenIsSentWithoutLogin()
    {
        var recorder = new RecordingHandler();
        var token = Jwt(DateTimeOffset.UtcNow.AddHours(1));
        var client = TokenClient(recorder, token);

        await client.GetItemMappingsAsync("ASYCB3S0", CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal("/workspace-mappings/ASYCB3S0", request.Path);
        Assert.Equal(token, request.Authorization);
    }

    [Fact]
    public async Task RejectedBearerTokenFailsWithClearMessage()
    {
        var recorder = new RecordingHandler { Status = HttpStatusCode.Unauthorized };
        var client = TokenClient(recorder, Jwt(DateTimeOffset.UtcNow.AddHours(1)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetItemMappingsAsync("ASYCB3S0", CancellationToken.None));

        Assert.Contains("rejected the bearer token (401)", ex.Message);
        Assert.Single(recorder.Requests);
    }

    [Fact]
    public async Task TestConnectionWithBearerTokenProbesAndReportsExpiry()
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        var ok = await TokenClient(new RecordingHandler { Status = HttpStatusCode.NotFound }, Jwt(expiry)).CheckAsync(CancellationToken.None);
        Assert.True(ok.Healthy, ok.Detail);
        Assert.Contains("valid until", ok.Detail);

        var expiredRecorder = new RecordingHandler();
        var expired = await TokenClient(expiredRecorder, Jwt(DateTimeOffset.UtcNow.AddMinutes(-5))).CheckAsync(CancellationToken.None);
        Assert.False(expired.Healthy);
        Assert.Contains("expired", expired.Detail);
        Assert.Empty(expiredRecorder.Requests);

        var forbidden = await TokenClient(new RecordingHandler { Status = HttpStatusCode.Forbidden }, Jwt(expiry)).CheckAsync(CancellationToken.None);
        Assert.False(forbidden.Healthy);
    }

    [Fact]
    public void JwtInfoReadsExpiryAndToleratesNonJwtTokens()
    {
        var expiry = new DateTimeOffset(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

        Assert.Equal(expiry, JwtInfo.Expiry(Jwt(expiry)));
        Assert.Null(JwtInfo.Expiry("opaque-token"));
        Assert.Equal("abc", JwtInfo.Normalize("  Bearer abc "));
    }

    private static MigrationToolClient TokenClient(RecordingHandler recorder, string token)
    {
        var options = MsOptions.Create(new MigrationToolOptions { BearerToken = token });
        return new MigrationToolClient(new HttpClient(new ReadOnlyGuardHandler(options) { InnerHandler = recorder }) { BaseAddress = new Uri("http://mt.test") }, options);
    }

    /// <summary>Unsigned test JWT; optionally prefixed with "Bearer " as users may paste it.</summary>
    private static string Jwt(DateTimeOffset expiry)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return "Bearer " + B64("""{"alg":"HS256","typ":"JWT"}""") + "." + B64($$"""{"sub":"u","exp":{{expiry.ToUnixTimeSeconds()}}}""") + ".sig";
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Authorization)> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method, path, request.Headers.Authorization?.ToString()));
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            if (Status != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(Status);
            }
            var offset = int.Parse(System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["offset"] ?? "0");
            var body = path switch
            {
                "/auth/login" => """{"access_token":"t","token_type":"bearer","refresh_token":""}""",
                "/export/workspace-mappings" => offset == 0
                    ? """{"items":[{"itemId":"A"},{"itemId":"B"}],"total":3}"""
                    : """{"items":[{"itemId":"C"}],"total":3}""",
                "/bom/items/by-ids" => """[{"itemId":"A5TBL01"}]""",
                "/bom/items" => offset == 0 ? """[{"itemId":"I1"},{"itemId":"I2"}]""" : """[{"itemId":"I3"}]""",
                _ => "[]"
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
