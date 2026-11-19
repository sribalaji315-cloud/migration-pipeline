using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Api.Services;
using Haworth.MdPipeline.Core.Enums;
using Haworth.MdPipeline.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Haworth.MdPipeline.Api.Tests;

public sealed class ApiFactory(string fixturesSubdir = "") : WebApplicationFactory<Program>
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "mdp-api-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Root);
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={Path.Combine(Root, "md_pipeline.db")}");
        builder.UseSetting("Pipeline:DataDir", Path.Combine(Root, "data"));
        builder.UseSetting("Pipeline:FixturesDir", Path.Combine(AppContext.BaseDirectory, "fixtures", fixturesSubdir));
        builder.UseSetting("Pipeline:DuckDbMemoryLimit", "1GB");
        builder.UseSetting("Pipeline:DuckDbThreads", "2");
        builder.UseSetting("Swing:Enabled", "false");
        builder.UseSetting("MigrationTool:UseFixtures", "true");
        builder.UseSetting("AutoMigrate", "true");
        builder.UseSetting("SeedIdentity", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public sealed class PipelineApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    private async Task<HttpClient> ClientAsync(string? role)
    {
        var client = factory.CreateClient();
        if (role is null)
        {
            return client;
        }

        var login = await client.PostAsJsonAsync("/api/auth/login", new AuthLoginRequest($"{role.ToLowerInvariant()}@mdpipeline.local", SeedData.DevPassword));
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<AuthLoginResponse>(Json))!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<RunDto> WaitForRunAsync(HttpClient client, string runId)
    {
        for (var i = 0; i < 300; i++)
        {
            var run = (await client.GetFromJsonAsync<RunDto>($"/api/runs/{runId}", Json))!;
            if (run.Status is RunStatus.Completed or RunStatus.Failed)
            {
                return run;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Run {runId} did not finish.");
    }

    private static async Task<RunDto> SyncAsync(HttpClient client, string query = "")
    {
        var response = await client.PostAsJsonAsync($"/api/sync{query}", new RunRequestDto(new DateOnly(2026, 11, 19), null));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var queued = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        return await WaitForRunAsync(client, queued.RunId);
    }

    [Fact]
    public async Task HealthIsAnonymousAndGreenInFixtureMode()
    {
        var client = await ClientAsync(null);

        var health = await client.GetFromJsonAsync<HealthDto>("/api/health", Json);

        Assert.True(health!.Healthy);
        Assert.Equal(["MigrationTool", "Swing", "DataDir"], health.Checks.Select(c => c.Name));
    }

    [Theory]
    [InlineData(null, "/api/sync", HttpStatusCode.Unauthorized)]
    [InlineData("Viewer", "/api/sync", HttpStatusCode.Forbidden)]
    [InlineData("Viewer", "/api/runs/analysis", HttpStatusCode.Forbidden)]
    [InlineData("Analyst", "/api/sync?full=true", HttpStatusCode.Forbidden)]
    public async Task RolesAreEnforced(string? role, string path, HttpStatusCode expected)
    {
        var client = await ClientAsync(role);

        var response = await client.PostAsJsonAsync(path, new RunRequestDto(null, null));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AnalysisWithoutBronzeIsRejected()
    {
        var client = await ClientAsync("Analyst");

        var response = await client.PostAsJsonAsync("/api/runs/analysis", new RunRequestDto(null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SyncPublishesBronzeAndRunsSwingNormalization()
    {
        var client = await ClientAsync("Analyst");

        var run = await SyncAsync(client);

        Assert.True(run.Status == RunStatus.Completed, run.ErrorMessage);
        Assert.Equal(1, run.BronzeVersion);
        Assert.Equal(["SyncSwingBomStep", "SyncSwingItemStep", "SyncMigrationToolStep", "NormalizeSwingStep"], run.Steps.Where(s => s.Status != StepStatus.Skipped).Select(s => s.Step));
        Assert.Equal(10, run.Steps.Count(s => s.Status == StepStatus.Skipped));
        Assert.All(run.Steps.Take(4), s => Assert.Equal(StepStatus.Completed, s.Status));

        var bronze = Assert.Single((await client.GetFromJsonAsync<List<BronzeVersionDto>>("/api/sync/versions", Json))!);
        Assert.Equal(BronzeVersionStatus.Published, bronze.Status);
        Assert.Equal(17, bronze.SwingBomRows);
        Assert.Equal(13, bronze.SwingItems);
        Assert.Equal(3, bronze.MappingRows);
        Assert.NotNull(bronze.SwingBomHash);

        var type = (await client.GetFromJsonAsync<ItemTypeDto>("/api/items/00123/type", Json))!;
        Assert.Equal(("GSAR00", "ARVAKZ", "0", "FIXED"), (type.SourceTable, type.SourceColumn, type.RawCode, type.SourceType));

        var lines = (await client.GetFromJsonAsync<List<BomLineDto>>("/api/boms/A5TBL01/lines", Json))!;
        Assert.True(lines.Single(l => l.ChildItemId == "PSEUDOCHILD").IsPseudoLine);
        Assert.All(lines, l => Assert.Equal("GSST00", l.SourceTable));

        var overview = (await client.GetFromJsonAsync<OverviewDto>("/api/overview", Json))!;
        Assert.Contains(overview.SwingIssues, i => i is { IssueType: "BOM_CYCLE", Severity: "Block", Count: 2 });

        var second = await SyncAsync(client);
        Assert.Equal((RunStatus.Completed, 2, (SyncMode?)SyncMode.Incremental), (second.Status, second.BronzeVersion, second.SyncMode));
    }

    [Theory]
    [InlineData("Analyst")]
    [InlineData("Approver")]
    public async Task SwingImportIsAdminOnly(string role)
    {
        var client = await ClientAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/swing/import", new RunRequestDto(null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/swing/source")).StatusCode);
    }

    [Fact]
    public async Task SwingImportExtractsSwingAndCarriesMigrationToolDataForward()
    {
        var admin = await ClientAsync("Admin");
        await SyncAsync(admin);

        var source = (await admin.GetFromJsonAsync<SwingSourceDto>("/api/swing/source", Json))!;
        Assert.Equal(("Fixture", 1), (source.Mode, source.LatestSnapshot!.Version));
        Assert.True((await (await admin.PostAsync("/api/swing/test-connection", null)).Content.ReadFromJsonAsync<HealthCheckDto>(Json))!.Healthy);

        var response = await admin.PostAsJsonAsync("/api/swing/import", new RunRequestDto(new DateOnly(2026, 11, 19), null));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var run = await WaitForRunAsync(admin, (await response.Content.ReadFromJsonAsync<RunDto>(Json))!.RunId);

        Assert.True(run.Status == RunStatus.Completed, run.ErrorMessage);
        Assert.Equal(SyncScope.SwingOnly, run.SyncScope);
        Assert.Equal(["SyncSwingBomStep", "SyncSwingItemStep", "CarryForwardMigrationToolStep", "NormalizeSwingStep"], run.Steps.Where(s => s.Status != StepStatus.Skipped).Select(s => s.Step));

        var snapshot = (await admin.GetFromJsonAsync<SwingSourceDto>("/api/swing/source", Json))!.LatestSnapshot!;
        Assert.Equal((2, (int?)1, 3L, 3, 17L), (snapshot.Version, snapshot.MtCarriedFromVersion, snapshot.MappingRows, snapshot.MtItems, snapshot.SwingBomRows));
    }

    [Fact]
    public async Task MigrationToolImportRequiresSwingDataFirst()
    {
        var admin = await ClientAsync("Admin");

        var response = await admin.PostAsJsonAsync("/api/migration-tool/import", new RunRequestDto(null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await (await ClientAsync("Approver")).PostAsJsonAsync("/api/migration-tool/import", new RunRequestDto(null, null))).StatusCode);
    }

    [Fact]
    public async Task MigrationToolImportSyncsMappingsAndCarriesSwingDataForward()
    {
        var admin = await ClientAsync("Admin");
        var swing = await WaitForRunAsync(admin,
            (await (await admin.PostAsJsonAsync("/api/swing/import", new RunRequestDto(new DateOnly(2026, 11, 19), null))).Content.ReadFromJsonAsync<RunDto>(Json))!.RunId);
        Assert.True(swing.Status == RunStatus.Completed, swing.ErrorMessage);

        var source = (await admin.GetFromJsonAsync<MigrationToolSourceDto>("/api/migration-tool/source", Json))!;
        Assert.Equal(("Fixture", 0L), (source.Mode, source.LatestSnapshot!.MappingRows));
        Assert.True((await (await admin.PostAsync("/api/migration-tool/test-connection", null)).Content.ReadFromJsonAsync<HealthCheckDto>(Json))!.Healthy);

        var response = await admin.PostAsJsonAsync("/api/migration-tool/import?full=true", new RunRequestDto(new DateOnly(2026, 11, 19), null));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var run = await WaitForRunAsync(admin, (await response.Content.ReadFromJsonAsync<RunDto>(Json))!.RunId);

        Assert.True(run.Status == RunStatus.Completed, run.ErrorMessage);
        Assert.Equal((SyncScope.MigrationToolOnly, (SyncMode?)SyncMode.Full), (run.SyncScope, run.SyncMode));
        Assert.Equal(["CarryForwardSwingStep", "SyncMigrationToolStep", "NormalizeSwingStep"], run.Steps.Where(s => s.Status != StepStatus.Skipped).Select(s => s.Step));

        var after = (await admin.GetFromJsonAsync<MigrationToolSourceDto>("/api/migration-tool/source", Json))!;
        var snapshot = after.LatestSnapshot!;
        Assert.Equal((2, (int?)1, (int?)null, 3L, 3), (snapshot.Version, snapshot.SwingCarriedFromVersion, snapshot.MtCarriedFromVersion, snapshot.MappingRows, snapshot.MtItems));
        Assert.Equal((17L, 13), (snapshot.SwingBomRows, snapshot.SwingItems));
        Assert.Equal(run.RunId, after.LastImport!.RunId);
    }

    [Fact]
    public async Task ListsSelectableBomsAndPreviewsScope()
    {
        var admin = await ClientAsync("Admin");
        await SyncAsync(admin);

        var tops = (await admin.GetFromJsonAsync<PagedResult<BomSummaryDto>>("/api/boms", Json))!;
        Assert.Equal(["A5TBL01"], tops.Items.Select(b => b.ItemId));
        Assert.Equal("Tisch Basis", tops.Items[0].Description);

        var all = (await admin.GetFromJsonAsync<PagedResult<BomSummaryDto>>("/api/boms?topOnly=false", Json))!;
        Assert.Equal(["00123", "A5TBL01", "CYC1", "CYC2"], all.Items.Select(b => b.ItemId));
        var search = (await admin.GetFromJsonAsync<PagedResult<BomSummaryDto>>("/api/boms?topOnly=false&q=cyc", Json))!;
        Assert.Equal((2, 2), (search.Total, search.Items.Count));

        var preview = await (await admin.PostAsJsonAsync("/api/boms/scope-preview", new BomScopePreviewRequestDto(["A5TBL01", "NOPE"])))
            .Content.ReadFromJsonAsync<BomScopePreviewDto>(Json);
        Assert.Equal((2, 16, "NOPE"), (preview!.Selected, preview.Items, Assert.Single(preview.Unknown)));
    }

    [Fact]
    public async Task BomScopedMigrationToolImportsOnlySelectedExplosionsAndKeepsOthers()
    {
        var admin = await ClientAsync("Admin");
        var swing = await WaitForRunAsync(admin,
            (await (await admin.PostAsJsonAsync("/api/swing/import", new RunRequestDto(new DateOnly(2026, 11, 19), null))).Content.ReadFromJsonAsync<RunDto>(Json))!.RunId);
        Assert.True(swing.Status == RunStatus.Completed, swing.ErrorMessage);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/migration-tool/import", new MigrationToolImportRequestDto(null, null, ["NOPE"]))).StatusCode);

        async Task<(RunDto Run, BronzeVersionDto Snapshot)> ImportAsync(params string[] boms)
        {
            var response = await admin.PostAsJsonAsync("/api/migration-tool/import", new MigrationToolImportRequestDto(new DateOnly(2026, 11, 19), null, boms));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var run = await WaitForRunAsync(admin, (await response.Content.ReadFromJsonAsync<RunDto>(Json))!.RunId);
            Assert.True(run.Status == RunStatus.Completed, run.ErrorMessage);
            return (run, (await admin.GetFromJsonAsync<MigrationToolSourceDto>("/api/migration-tool/source", Json))!.LatestSnapshot!);
        }

        var (first, s1) = await ImportAsync("A5TBL01");
        Assert.Equal(["A5TBL01"], first.ScopeTopBoms);
        Assert.Equal((15, 3L, 3, 2), (s1.MtScopeItems, s1.MappingRows, s1.MtItems, s1.ChangedItems));

        var (_, s2) = await ImportAsync("CYC1");
        Assert.Equal(["CYC1"], s2.MtScopeTopBoms);
        Assert.Equal((2, 3L, 3, 0), (s2.MtScopeItems, s2.MappingRows, s2.MtItems, s2.ChangedItems));
    }

    [Fact]
    public async Task RunListIsPagedWithMaximumLimit()
    {
        var client = await ClientAsync("Viewer");

        var page = (await client.GetFromJsonAsync<PagedResult<RunDto>>("/api/runs?limit=1000", Json))!;

        Assert.Equal(500, page.Limit);
    }

    [Fact]
    public async Task AdminUploadsBluestarReferenceVersion()
    {
        var viewer = await ClientAsync("Viewer");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/reference/bluestar", ReferenceForm())).StatusCode);

        var admin = await ClientAsync("Admin");
        var response = await admin.PostAsync("/api/reference/bluestar", ReferenceForm());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = Assert.Single((await admin.GetFromJsonAsync<List<ReferenceVersionDto>>("/api/reference/bluestar/versions", Json))!);
        Assert.Equal(1, version.Version);
        Assert.Equal(5, version.Files.Count);
        Assert.All(version.Files, f => Assert.Equal(1, f.Rows));
    }

    private static MultipartFormDataContent ReferenceForm()
    {
        var form = new MultipartFormDataContent();
        foreach (var spec in BluestarContract.Files)
        {
            using var book = new XLWorkbook();
            var sheet = book.AddWorksheet("Sheet1");
            for (var i = 0; i < spec.Columns.Count; i++)
            {
                sheet.Cell(1, i + 1).Value = spec.Columns[i];
                sheet.Cell(2, i + 1).Value = $"v{i}";
            }

            var stream = new MemoryStream();
            book.SaveAs(stream);
            form.Add(new ByteArrayContent(stream.ToArray()), spec.Kind.ToString(), spec.FileName);
        }

        return form;
    }
}
