using System.Security.Claims;
using Haworth.MdPipeline.Api.Contracts;
using Haworth.MdPipeline.Api.Services;
using Haworth.MdPipeline.Core.Enums;

namespace Haworth.MdPipeline.Api.Endpoints;

public static class PipelineEndpoints
{
    public static string ActorId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(ClaimTypes.Email) ?? "system";

    public static void MapPipelineEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", (AuthLoginRequest request, AuthService auth) => auth.LoginAsync(request))
            .AllowAnonymous();

        app.MapGet("/api/health", async (HealthService health, CancellationToken ct) =>
        {
            var result = await health.CheckAsync(ct);
            return result.Healthy ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();

        var api = app.MapGroup("/api").RequireAuthorization(Policies.Viewers);

        api.MapGet("/overview", (OverviewService overview, CancellationToken ct) => overview.GetAsync(ct));

        api.MapPost("/sync", async (bool? full, RunRequestDto? request, ClaimsPrincipal user, RunService runs, CancellationToken ct) =>
            {
                if (full == true && !user.IsInRole(Policies.Admin))
                {
                    return Results.Forbid();
                }

                return Results.Accepted(null, await runs.QueueSyncAsync(full == true, request, ActorId(user), ct));
            })
            .RequireAuthorization(Policies.Analysts);

        api.MapGet("/sync/versions", (RunService runs, CancellationToken ct) => runs.BronzeVersionsAsync(ct));

        api.MapPost("/runs/analysis", async (RunRequestDto? request, ClaimsPrincipal user, RunService runs, CancellationToken ct) =>
                Results.Accepted(null, await runs.QueueAnalysisAsync(request, ActorId(user), ct)))
            .RequireAuthorization(Policies.Analysts);

        api.MapGet("/runs", (int? limit, int? offset, RunService runs, CancellationToken ct) => runs.ListAsync(limit ?? 50, offset ?? 0, ct));
        api.MapGet("/runs/{runId}", (string runId, RunService runs, CancellationToken ct) => runs.GetAsync(runId, ct));

        api.MapGet("/items/{itemId}/type", async (string itemId, SilverQueryService silver, AnalysisQueryService analysis, CancellationToken ct) =>
            await analysis.ExtendItemTypeAsync(await silver.ItemTypeAsync(itemId, ct), ct));
        api.MapGet("/items/{itemId}/class", (string itemId, AnalysisQueryService analysis, CancellationToken ct) => analysis.ItemClassAsync(itemId, ct));
        api.MapGet("/items/{itemId}/class/{attributeId}/missing-values", (string itemId, string attributeId, AnalysisQueryService analysis, CancellationToken ct) =>
            analysis.MissingValuesAsync(itemId, attributeId, ct));
        api.MapGet("/boms/{itemId}/lines", (string itemId, SilverQueryService silver, CancellationToken ct) => silver.BomLinesAsync(itemId, ct));
        api.MapGet("/boms/readiness", (string? q, string? status, int? limit, int? offset, AnalysisQueryService analysis, CancellationToken ct) =>
            analysis.BomReadinessAsync(q, status, limit ?? 50, offset ?? 0, ct));
        api.MapGet("/boms/{topItemId}/tree", (string topItemId, AnalysisQueryService analysis, CancellationToken ct) => analysis.BomTreeAsync(topItemId, ct));

        api.MapGet("/issues/summary", (ReviewService review, CancellationToken ct) => review.SummaryAsync(ct));
        api.MapGet("/issues/groups", (Severity? severity, FixSource? fixSource, string? issueType, IssueStatus? status, string? q, string? bom,
                int? limit, int? offset, ReviewService review, CancellationToken ct) =>
            review.GroupsAsync(new ReviewService.IssueFilter(severity, fixSource, issueType, status, q, bom), limit ?? 50, offset ?? 0, ct));
        api.MapGet("/issues/groups/{id:guid}", (Guid id, ReviewService review, CancellationToken ct) => review.GroupAsync(id, ct));
        api.MapGet("/issues/groups/{id:guid}/items", (Guid id, int? limit, int? offset, ReviewService review, CancellationToken ct) =>
            review.GroupItemsAsync(id, limit ?? 50, offset ?? 0, ct));
        api.MapPost("/issues/groups/{id:guid}/acknowledge", (Guid id, AcknowledgeIssueRequestDto? request, ClaimsPrincipal user, ReviewService review, CancellationToken ct) =>
                review.AcknowledgeAsync(id, request?.Note, ActorId(user), ct))
            .RequireAuthorization(Policies.Analysts);

        api.MapGet("/type-changes", (string? q, TypeDecisionStatus? decision, int? limit, int? offset, AnalysisQueryService analysis, CancellationToken ct) =>
            analysis.TypeChangesAsync(q, decision, limit ?? 50, offset ?? 0, ct));
        api.MapPost("/type-changes/decide", (TypeDecisionRequestDto request, ClaimsPrincipal user, ReviewService review, CancellationToken ct) =>
                review.DecideAsync(request, ActorId(user), ct))
            .RequireAuthorization(Policies.Approvers);

        api.MapPost("/waves", async (CreateWaveRequestDto request, ClaimsPrincipal user, WaveService waves, CancellationToken ct) =>
            {
                var wave = await waves.CreateAsync(request, ActorId(user), ct);
                return Results.Created($"/api/waves/{wave.WaveId}", wave);
            })
            .RequireAuthorization(Policies.Analysts);
        api.MapGet("/waves", (int? limit, int? offset, WaveService waves, CancellationToken ct) => waves.ListAsync(limit ?? 50, offset ?? 0, ct));
        api.MapGet("/waves/{waveId}", (string waveId, WaveService waves, CancellationToken ct) => waves.GetAsync(waveId, ct));
        api.MapPost("/waves/{waveId}/refresh", (string waveId, ClaimsPrincipal user, WaveService waves, CancellationToken ct) =>
                waves.RefreshAsync(waveId, ActorId(user), ct))
            .RequireAuthorization(Policies.Analysts);
        api.MapGet("/waves/{waveId}/gate", (string waveId, WaveService waves, CancellationToken ct) => waves.GateAsync(waveId, ct));
        api.MapPost("/waves/{waveId}/export", async (string waveId, ClaimsPrincipal user, WaveService waves, CancellationToken ct) =>
                Results.Accepted(null, await waves.QueueExportAsync(waveId, ActorId(user), ct)))
            .RequireAuthorization(Policies.Approvers);
        api.MapGet("/waves/{waveId}/files", (string waveId, WaveService waves, CancellationToken ct) => waves.FilesAsync(waveId, ct));
        api.MapGet("/waves/{waveId}/files/{file}", async (string waveId, string file, WaveService waves, CancellationToken ct) =>
        {
            var path = await waves.FilePathAsync(waveId, file, ct);
            var contentType = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? "application/json"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return Results.File(path, contentType, file);
        });

        api.MapGet("/swing/source", (SourceImportService sources, CancellationToken ct) => sources.GetSwingAsync(ct))
            .RequireAuthorization(Policies.Administrators);
        api.MapPost("/swing/test-connection", (SourceImportService sources, CancellationToken ct) => sources.TestSwingAsync(ct))
            .RequireAuthorization(Policies.Administrators);
        api.MapPost("/swing/import", async (RunRequestDto? request, ClaimsPrincipal user, RunService runs, CancellationToken ct) =>
                Results.Accepted(null, await runs.QueueSwingImportAsync(request, ActorId(user), ct)))
            .RequireAuthorization(Policies.Administrators);

        api.MapGet("/migration-tool/source", (SourceImportService sources, CancellationToken ct) => sources.GetMigrationToolAsync(ct))
            .RequireAuthorization(Policies.Administrators);
        api.MapPost("/migration-tool/test-connection", (SourceImportService sources, CancellationToken ct) => sources.TestMigrationToolAsync(ct))
            .RequireAuthorization(Policies.Administrators);
        api.MapPost("/migration-tool/import", async (bool? full, MigrationToolImportRequestDto? request, ClaimsPrincipal user, RunService runs, CancellationToken ct) =>
                Results.Accepted(null, await runs.QueueMigrationToolImportAsync(full == true, request, ActorId(user), ct)))
            .RequireAuthorization(Policies.Administrators);

        api.MapGet("/boms", (string? q, bool? topOnly, int? limit, int? offset, SilverQueryService silver, CancellationToken ct) =>
            silver.BomsAsync(q, topOnly ?? true, limit ?? 50, offset ?? 0, ct));
        api.MapPost("/boms/scope-preview", (BomScopePreviewRequestDto request, SilverQueryService silver, CancellationToken ct) =>
            silver.ScopePreviewAsync(request.TopBoms ?? [], ct));

        api.MapPost("/reference/bluestar", async (HttpRequest request, ClaimsPrincipal user, ReferenceService reference, CancellationToken ct) =>
            {
                var form = await request.ReadFormAsync(ct);
                return Results.Created((string?)null, await reference.UploadAsync(form.Files, ActorId(user), ct));
            })
            .RequireAuthorization(Policies.Administrators)
            .DisableAntiforgery();

        api.MapGet("/reference/bluestar/versions", (ReferenceService reference, CancellationToken ct) => reference.VersionsAsync(ct));
    }
}
