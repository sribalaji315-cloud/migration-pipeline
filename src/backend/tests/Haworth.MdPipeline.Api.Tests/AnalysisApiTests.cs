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

namespace Haworth.MdPipeline.Api.Tests;

/// <summary>End to end on tests/fixtures/scenario: reference upload → sync + analysis → review → wave gate → export.</summary>
public sealed class AnalysisApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly ApiFactory factory = new("scenario");

    public void Dispose() => factory.Dispose();

    private async Task<HttpClient> ClientAsync(string role)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new AuthLoginRequest($"{role.ToLowerInvariant()}@mdpipeline.local", SeedData.DevPassword));
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthLoginResponse>(Json))!.Token);
        return client;
    }

    private static async Task<RunDto> WaitAsync(HttpClient client, HttpResponseMessage queued)
    {
        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        var runId = (await queued.Content.ReadFromJsonAsync<RunDto>(Json))!.RunId;
        for (var i = 0; i < 300; i++)
        {
            var run = (await client.GetFromJsonAsync<RunDto>($"/api/runs/{runId}", Json))!;
            if (run.Status is RunStatus.Completed or RunStatus.Failed)
            {
                Assert.True(run.Status == RunStatus.Completed, run.ErrorMessage);
                return run;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Run {runId} did not finish.");
    }

    private async Task<HttpClient> AnalysedAsync()
    {
        var admin = await ClientAsync("Admin");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync("/api/reference/bluestar", ScenarioReference())).StatusCode);
        var run = await WaitAsync(admin, await admin.PostAsJsonAsync("/api/sync", new RunRequestDto(new DateOnly(2026, 11, 19), null)));
        Assert.DoesNotContain(run.Steps, s => s.Status != StepStatus.Completed);
        Assert.Contains(run.Steps, s => s.Step == "ReadinessStep");
        return admin;
    }

    [Fact]
    public async Task ExposesClassModelReadinessIssuesAndTypeChanges()
    {
        var client = await AnalysedAsync();

        var mid = (await client.GetFromJsonAsync<ItemClassDto>("/api/items/MID/class", Json))!;
        Assert.Equal("CL_MID", mid.ClassId);
        var fabric = Assert.Single(mid.Attributes, a => a.AttributeId == "ColorOfFabric");
        Assert.Equal(("Local", "Bluestar", "Global_UpholsteredColors", 2L, "Go"),
            (fabric.ValueSource, fabric.AttributeSource, fabric.GlobalVariable, fabric.ValueListSize, fabric.Status));
        var inherited = Assert.Single(mid.Attributes, a => a.AttributeId == "ColorLevel3");
        Assert.Equal(("Inherited", "TOP"), (inherited.ValueSource, inherited.InheritedFrom));

        var missing = (await client.GetFromJsonAsync<MissingValuesDto>("/api/items/TOP2/class/ColorLevel3/missing-values", Json))!;
        Assert.Equal(["F9999"], missing.Values);
        Assert.True(missing.IsList);

        var type = (await client.GetFromJsonAsync<ItemTypeDto>("/api/items/LEAF/type", Json))!;
        Assert.Equal(("CONFIGURABLE", true, "ColorLevel3", (TypeDecisionStatus?)TypeDecisionStatus.Pending), (type.ResolvedType, type.TypeChanged, type.InheritedAttrs, type.Decision));

        var readiness = (await client.GetFromJsonAsync<PagedResult<BomReadinessDto>>("/api/boms/readiness", Json))!;
        Assert.Equal(["CLEAN:DECISION", "ORPHAN:READY", "TOP:BLOCKED", "TOP2:BLOCKED"], readiness.Items.Select(b => $"{b.TopItemId}:{b.Status}"));
        Assert.Single((await client.GetFromJsonAsync<PagedResult<BomReadinessDto>>("/api/boms/readiness?status=ready", Json))!.Items);

        var tree = (await client.GetFromJsonAsync<BomTreeDto>("/api/boms/TOP/tree", Json))!;
        Assert.Equal(["LEAF", "MID", "TOP"], tree.Nodes.Select(n => n.ItemId));
        Assert.Equal(["MID>LEAF", "TOP>MID"], tree.Edges.Select(e => $"{e.ParentItemId}>{e.ChildItemId}"));

        var groups = (await client.GetFromJsonAsync<PagedResult<IssueGroupDto>>("/api/issues/groups?fixSource=BluestarValueList", Json))!;
        var valueList = Assert.Single(groups.Items);
        Assert.Equal(("VALUES_NOT_IN_VALUELIST", Severity.Block, IssueStatus.Open, 1), (valueList.IssueType, valueList.Severity, valueList.Status, valueList.AffectedBoms));
        var detail = (await client.GetFromJsonAsync<PagedResult<IssueItemDto>>($"/api/issues/groups/{valueList.Id}/items", Json))!;
        Assert.Equal("F9999", Assert.Single(detail.Items).ValueCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/issues/groups/{valueList.Id}/acknowledge", new AcknowledgeIssueRequestDto("ok"))).StatusCode);
        var forTop = (await client.GetFromJsonAsync<PagedResult<IssueGroupDto>>("/api/issues/groups?bom=TOP", Json))!;
        Assert.DoesNotContain(forTop.Items, g => g.IssueType == "VALUES_NOT_IN_VALUELIST");
        var all = (await client.GetFromJsonAsync<PagedResult<IssueGroupDto>>("/api/issues/groups", Json))!;
        Assert.Equal(all.Items.Select(g => g.Severity).Order(), all.Items.Select(g => g.Severity));

        var changes = (await client.GetFromJsonAsync<PagedResult<TypeChangeDto>>("/api/type-changes", Json))!;
        Assert.Equal(["CLEANLEAF", "LEAF", "LEAF2", "MID"], changes.Items.Select(c => c.ItemId));
        Assert.All(changes.Items, c => Assert.Equal(TypeDecisionStatus.Pending, c.Decision));

        var overview = (await client.GetFromJsonAsync<OverviewDto>("/api/overview", Json))!;
        Assert.Equal((1, 1, 2), (overview.Analysis!.Boms.Ready, overview.Analysis.Boms.Decision, overview.Analysis.Boms.Blocked));
        Assert.Equal(4, overview.Analysis.PendingTypeDecisions);
    }

    [Fact]
    public async Task WaveGateBlocksUntilTypeChangeIsApprovedThenExportsAllSheets()
    {
        var admin = await AnalysedAsync();
        var analyst = await ClientAsync("Analyst");
        var approver = await ClientAsync("Approver");

        Assert.Equal(HttpStatusCode.BadRequest, (await analyst.PostAsJsonAsync("/api/waves", new CreateWaveRequestDto("x", ["MID"], null))).StatusCode);
        var created = await analyst.PostAsJsonAsync("/api/waves", new CreateWaveRequestDto("Tables wave 1", ["CLEAN"], null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var wave = (await created.Content.ReadFromJsonAsync<WaveDto>(Json))!;
        Assert.Equal(("W-01", 2, WaveStatus.Draft), (wave.WaveId, wave.Items, wave.Status));

        var gate = (await analyst.GetFromJsonAsync<WaveGateDto>("/api/waves/W-01/gate", Json))!;
        Assert.False(gate.CanExport);
        Assert.Equal(["TypeChangesApproved:1"], gate.Checks.Where(c => !c.Passed).Select(c => $"{c.Name}:{c.Count}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await analyst.PostAsync("/api/waves/W-01/export", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await approver.PostAsync("/api/waves/W-01/export", null)).StatusCode);
        Assert.Equal(WaveStatus.Blocked, (await analyst.GetFromJsonAsync<WaveDto>("/api/waves/W-01", Json))!.Status);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await analyst.PostAsJsonAsync("/api/type-changes/decide", new TypeDecisionRequestDto(["CLEANLEAF"], TypeDecisionStatus.Approved, null))).StatusCode);
        var decided = await approver.PostAsJsonAsync("/api/type-changes/decide", new TypeDecisionRequestDto(["CLEANLEAF", "NOPE"], TypeDecisionStatus.Approved, null));
        var result = (await decided.Content.ReadFromJsonAsync<TypeDecisionResultDto>(Json))!;
        Assert.Equal((1, "NOPE"), (result.Updated, Assert.Single(result.NotFound)));

        Assert.True((await analyst.GetFromJsonAsync<WaveGateDto>("/api/waves/W-01/gate", Json))!.CanExport);
        var export = await WaitAsync(approver, await approver.PostAsync("/api/waves/W-01/export", null));
        Assert.Equal(["WaveGoldStep", "WaveExportStep"], export.Steps.Select(s => s.Step));
        Assert.Equal(WaveStatus.Exported, (await analyst.GetFromJsonAsync<WaveDto>("/api/waves/W-01", Json))!.Status);

        var files = (await analyst.GetFromJsonAsync<List<WaveFileDto>>("/api/waves/W-01/files", Json))!;
        Assert.Equal(
        [
            "01_class.xlsx", "02_class_attribute.xlsx", "03_engineering_object.xlsx", "04_engineering_object_attribute_value.xlsx",
            "05_inheritance_link.xlsx", "06_eoav_condition.xlsx", "ISSUES_W-01.xlsx", "manifest.json"
        ], files.Select(f => f.Name));
        using (var book = new XLWorkbook(new MemoryStream(await analyst.GetByteArrayAsync("/api/waves/W-01/files/03_engineering_object.xlsx"))))
        {
            var sheet = book.Worksheet(1);
            Assert.Equal("ObjectId", sheet.Cell(1, 1).GetString());
            Assert.Equal(["CLEAN|CONFIGURABLE|FALSE", "CLEANLEAF|CONFIGURABLE|TRUE"],
                sheet.RowsUsed().Skip(1).Select(r => $"{r.Cell(1).GetString()}|{r.Cell(6).GetString()}|{r.Cell(7).GetString()}"));
        }

        using (var book = new XLWorkbook(new MemoryStream(await analyst.GetByteArrayAsync("/api/waves/W-01/files/05_inheritance_link.xlsx"))))
        {
            var row = book.Worksheet(1).Row(2);
            Assert.Equal("CL_CLEAN|CL_CLEANLEAF|ColorLevel3|CL_CLEAN",
                string.Join("|", Enumerable.Range(1, 4).Select(c => row.Cell(c).GetString())));
        }

        var manifest = JsonDocument.Parse(await analyst.GetStringAsync("/api/waves/W-01/files/manifest.json")).RootElement;
        Assert.Equal(2, manifest.GetProperty("wave").GetProperty("items").GetInt32());
        Assert.Equal("Global_Color", manifest.GetProperty("reusedGlobalVariables")[0].GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await analyst.GetAsync("/api/waves/W-01/files/..%5Cmd_pipeline.db")).StatusCode);

        var blocked = await analyst.PostAsJsonAsync("/api/waves", new CreateWaveRequestDto("Desks", ["TOP"], null));
        Assert.Equal(HttpStatusCode.Created, blocked.StatusCode);
        var blockedGate = (await analyst.GetFromJsonAsync<WaveGateDto>("/api/waves/W-02/gate", Json))!;
        Assert.False(blockedGate.Checks.Single(c => c.Name == "NoBlockingOrReviewIssues").Passed);

        // A new analysis sees the export (EXPORTED) and keeps the approval, which still matches.
        await WaitAsync(admin, await admin.PostAsJsonAsync("/api/runs/analysis", new RunRequestDto(new DateOnly(2026, 11, 19), null)));
        var readiness = (await analyst.GetFromJsonAsync<PagedResult<BomReadinessDto>>("/api/boms/readiness?q=CLEAN", Json))!;
        Assert.Equal("EXPORTED", Assert.Single(readiness.Items).Status);
        var gateAfter = (await analyst.GetFromJsonAsync<WaveGateDto>("/api/waves/W-01/gate", Json))!;
        Assert.True(gateAfter.CanExport, string.Join(", ", gateAfter.Checks.Where(c => !c.Passed).Select(c => c.Name)));
    }

    private static MultipartFormDataContent ScenarioReference()
    {
        var form = new MultipartFormDataContent();
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "scenario", "bluestar");
        foreach (var spec in BluestarContract.Files)
        {
            var lines = File.ReadAllLines(Path.Combine(dir, spec.BronzeTable + ".csv"));
            using var book = new XLWorkbook();
            var sheet = book.AddWorksheet("Sheet1");
            for (var r = 0; r < lines.Length; r++)
            {
                var cells = SplitCsv(lines[r]);
                for (var c = 0; c < cells.Count; c++)
                {
                    sheet.Cell(r + 1, c + 1).Value = cells[c];
                }
            }

            var stream = new MemoryStream();
            book.SaveAs(stream);
            form.Add(new ByteArrayContent(stream.ToArray()), spec.Kind.ToString(), spec.FileName);
        }

        return form;
    }

    private static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
