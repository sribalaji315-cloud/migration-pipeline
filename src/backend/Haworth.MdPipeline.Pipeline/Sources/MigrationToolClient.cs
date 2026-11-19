using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Haworth.MdPipeline.Core.Interfaces;
using Haworth.MdPipeline.Pipeline.Options;
using Microsoft.Extensions.Options;

namespace Haworth.MdPipeline.Pipeline.Sources;

/// <summary>
/// Read-only Migration Tool client. Payload shapes for the requested bulk endpoint are assumptions
/// (see doc/requirements/plans/foundation-bronze-swing.md) until the Migration Tool team confirms them.
/// </summary>
public sealed class MigrationToolClient(HttpClient http, IOptions<MigrationToolOptions> options) : IMigrationToolClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly MigrationToolOptions mt = options.Value;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? accessToken;
    private string? refreshToken;

    public bool SupportsBulkMappings => mt.UseBulkEndpoint;

    public async IAsyncEnumerable<MtMappingRow> GetMappingsBulkAsync(DateTimeOffset? updatedSince, [EnumeratorCancellation] CancellationToken ct)
    {
        var offset = 0;
        while (true)
        {
            var query = $"/export/workspace-mappings?limit={mt.BulkPageSize}&offset={offset}";
            if (updatedSince is not null)
            {
                query += $"&updatedSince={Uri.EscapeDataString(updatedSince.Value.ToString("O"))}";
            }

            var page = await SendAsync<JsonElement>(() => new HttpRequestMessage(HttpMethod.Get, query), ct);
            if (page.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("Migration Tool returned an empty mapping page.");
            }

            var items = MtPayload.Rows(page).Select(r => MtPayload.Mapping(r)).ToList();
            foreach (var row in items)
            {
                yield return row;
            }

            offset += items.Count;
            var total = page.ValueKind == JsonValueKind.Object && page.TryGetProperty("total", out var t) && t.TryGetInt64(out var n) ? n : long.MaxValue;
            if (items.Count == 0 || offset >= total)
            {
                yield break;
            }
        }
    }

    public async Task<IReadOnlyList<MtMappingRow>> GetItemMappingsAsync(string itemId, CancellationToken ct)
    {
        var body = await SendAsync<JsonElement>(
            () => new HttpRequestMessage(HttpMethod.Get, $"/workspace-mappings/{Uri.EscapeDataString(itemId)}"), ct, allowNotFound: true);
        return body.ValueKind == JsonValueKind.Undefined ? [] : MtPayload.Rows(body).Select(r => MtPayload.Mapping(r, itemId)).ToList();
    }

    /// <summary>The endpoint expects a bare JSON array of item ids (an object body is rejected with 422).</summary>
    public async Task<IReadOnlyList<MtItemHeader>> GetItemsByIdsAsync(IReadOnlyList<string> itemIds, CancellationToken ct)
    {
        var body = await SendAsync<JsonElement>(
            () => new HttpRequestMessage(HttpMethod.Post, "/bom/items/by-ids") { Content = JsonContent.Create(itemIds, options: Json) }, ct);
        return body.ValueKind == JsonValueKind.Undefined ? [] : MtPayload.Rows(body).Select(MtPayload.Item).ToList();
    }

    /// <summary>Pages GET /bom/items?limit=&amp;offset= (bare array, stable order) until a short page.</summary>
    public async Task<IReadOnlyList<MtItemHeader>> GetAllItemsAsync(Func<long, Task>? progress, CancellationToken ct)
    {
        var all = new List<MtItemHeader>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var size = Math.Max(1, mt.ItemsPageSize);
        for (var offset = 0; ; offset += size)
        {
            var page = await SendAsync<JsonElement>(() => new HttpRequestMessage(HttpMethod.Get, $"/bom/items?limit={size}&offset={offset}"), ct);
            var rows = page.ValueKind == JsonValueKind.Undefined ? [] : MtPayload.Rows(page).Select(MtPayload.Item).ToList();
            all.AddRange(rows.Where(r => seen.Add(r.ItemId)));
            if (progress is not null)
            {
                await progress(all.Count);
            }

            if (rows.Count < size)
            {
                return all;
            }
        }
    }

    public async Task<SourceHealth> CheckAsync(CancellationToken ct)
    {
        try
        {
            if (!mt.HasBearerToken)
            {
                await EnsureTokenAsync(forceLogin: false, ct);
                return new SourceHealth("MigrationTool", true, $"Logged in to {mt.BaseUrl} as {mt.User}.");
            }

            var expiry = JwtInfo.Expiry(mt.BearerToken);
            if (expiry is { } exp && exp <= DateTimeOffset.UtcNow)
            {
                return new SourceHealth("MigrationTool", false, $"The bearer token expired at {exp:yyyy-MM-dd HH:mm} UTC. Put a new token in MigrationTool__BearerToken and restart.");
            }

            // The token is only proven by a real request: 200 or 404 mean it was accepted.
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/workspace-mappings/{Uri.EscapeDataString(mt.ProbeItemId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtInfo.Normalize(mt.BearerToken));
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new SourceHealth("MigrationTool", false, RejectedMessage(response.StatusCode));
            }

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                return new SourceHealth("MigrationTool", false, $"{mt.BaseUrl} answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var until = expiry is { } e ? $" Token valid until {e:yyyy-MM-dd HH:mm} UTC." : string.Empty;
            return new SourceHealth("MigrationTool", true, $"Bearer token accepted by {mt.BaseUrl}.{until}");
        }
        catch (Exception ex)
        {
            return new SourceHealth("MigrationTool", false, ex.Message);
        }
    }

    private async Task<T?> SendAsync<T>(Func<HttpRequestMessage> build, CancellationToken ct, bool allowNotFound = false)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await EnsureTokenAsync(forceLogin: false, ct);
            using var request = build();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct);

            if (mt.HasBearerToken && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(RejectedMessage(response.StatusCode));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                await RefreshAsync(token, ct);
                continue;
            }

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }

        throw new InvalidOperationException("Migration Tool authentication failed.");
    }

    private string RejectedMessage(HttpStatusCode status)
    {
        var expiry = JwtInfo.Expiry(mt.BearerToken);
        var hint = expiry is { } e && e <= DateTimeOffset.UtcNow ? $" It expired at {e:yyyy-MM-dd HH:mm} UTC." : " It may have expired or lack access.";
        return $"Migration Tool rejected the bearer token ({(int)status}).{hint} Put a new token in MigrationTool__BearerToken and restart.";
    }

    private async Task<string> EnsureTokenAsync(bool forceLogin, CancellationToken ct)
    {
        if (mt.HasBearerToken)
        {
            return JwtInfo.Normalize(mt.BearerToken);
        }

        if (!forceLogin && accessToken is not null)
        {
            return accessToken;
        }

        await tokenLock.WaitAsync(ct);
        try
        {
            if (forceLogin || accessToken is null)
            {
                if (!mt.HasLogin)
                {
                    throw new InvalidOperationException(
                        "No Migration Tool credentials: set MigrationTool__User and MigrationTool__Password (or MigrationTool__BearerToken) in .env and restart.");
                }

                // FastAPI OAuth2 password form: form fields, not JSON.
                using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = mt.User, ["password"] = mt.Password });
                using var response = await http.PostAsync("/auth/login", form, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
                {
                    throw new InvalidOperationException(
                        $"Migration Tool login as '{mt.User}' was rejected ({(int)response.StatusCode}): {await ErrorDetailAsync(response, ct)} Check MigrationTool__User / MigrationTool__Password in .env.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Migration Tool login failed: {mt.BaseUrl}/auth/login answered {(int)response.StatusCode} {response.ReasonPhrase}. {await ErrorDetailAsync(response, ct)}");
                }

                StoreTokens(await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct));
            }

            return accessToken!;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private async Task RefreshAsync(string staleToken, CancellationToken ct)
    {
        await tokenLock.WaitAsync(ct);
        try
        {
            if (accessToken != staleToken)
            {
                return;
            }

            if (refreshToken is not null)
            {
                using var response = await http.PostAsJsonAsync("/auth/refresh", new Dictionary<string, string> { ["refresh_token"] = refreshToken }, ct);
                if (response.IsSuccessStatusCode)
                {
                    StoreTokens(await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct));
                    return;
                }
            }

            accessToken = null;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    /// <summary>The API returns snake_case (<c>access_token</c>, <c>refresh_token</c>); camelCase is accepted too.</summary>
    private void StoreTokens(JsonElement tokens)
    {
        accessToken = Text(tokens, "access_token", "accessToken")
            ?? throw new InvalidOperationException("Migration Tool login returned no access token.");
        refreshToken = Text(tokens, "refresh_token", "refreshToken") ?? refreshToken;
    }

    private static string? Text(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } s)
            {
                return s;
            }
        }

        return null;
    }

    private static async Task<string> ErrorDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind == JsonValueKind.String ? detail.GetString()! : detail.GetRawText();
            }
        }
        catch (JsonException)
        {
        }

        return body.Length > 300 ? body[..300] : body;
    }
}

/// <summary>Blocks every verb except GET, POST /auth/* and POST /bom/items/by-ids, and caps concurrency.</summary>
public sealed class ReadOnlyGuardHandler(IOptions<MigrationToolOptions> options) : DelegatingHandler
{
    private readonly SemaphoreSlim gate = new(Math.Max(1, options.Value.MaxConcurrency));

    public static bool IsAllowed(HttpMethod method, string path) =>
        method == HttpMethod.Get
        || (method == HttpMethod.Post && (path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
                                          || string.Equals(path, "/bom/items/by-ids", StringComparison.OrdinalIgnoreCase)));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.IsAbsoluteUri == true ? request.RequestUri.AbsolutePath : request.RequestUri?.OriginalString.Split('?')[0] ?? string.Empty;
        if (!IsAllowed(request.Method, path))
        {
            throw new InvalidOperationException($"Migration Tool is read-only: {request.Method} {path} is not allowed.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
