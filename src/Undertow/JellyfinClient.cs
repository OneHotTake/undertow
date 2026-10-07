using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyembifier;

public sealed class UpstreamSettings
{
    public string Url { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string UserId { get; set; } = "";
    public string MovieLibraryId { get; set; } = "movies";
    public string SeriesLibraryId { get; set; } = "series";
}

public sealed class UpstreamItem
{
    public bool FromAnimeCatalog { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Overview { get; set; }
    public string? OfficialRating { get; set; }
    public string? SeriesName { get; set; }
    public string? CollectionType { get; set; }
    public int? ProductionYear { get; set; }
    public int? IndexNumber { get; set; }
    public int? ParentIndexNumber { get; set; }
    public long? RunTimeTicks { get; set; }
    public float? CommunityRating { get; set; }
    public DateTimeOffset? PremiereDate { get; set; }
    public Dictionary<string, string> ProviderIds { get; set; } = new();
    public List<string> Genres { get; set; } = new();
}

public sealed class CatalogPage
{
    public List<UpstreamItem> Items { get; set; } = new();
    public int TotalRecordCount { get; set; }
}

public sealed class JellyfinClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = TimeSpan.FromSeconds(65) };
    private readonly SemaphoreSlim authGate = new(1, 1);
    private string token = "";
    private string userId = "";
    public UpstreamSettings Settings { get; }
    public int MetadataRequests;
    public int PlaybackRequests;
    public int Authentications;
    private static JellyfinClient? instance;
    public static JellyfinClient Instance => instance ??= new();
    public static void Reset() => instance = null;

    private JellyfinClient()
    {
        Settings = ConnectionStore.Load();
        if (!Uri.TryCreate(Settings.Url, UriKind.Absolute, out var url) ||
            (url.Scheme != "http" && url.Scheme != "https") || url.UserInfo != "" || url.Query != "" || url.Fragment != "")
            throw new InvalidOperationException("Upstream must be an HTTP(S) server address without credentials or query.");
        http.BaseAddress = new Uri(Settings.Url.TrimEnd('/') + "/");
        if (!string.IsNullOrEmpty(Settings.AccessToken))
        {
            if (string.IsNullOrEmpty(Settings.UserId)) throw new InvalidOperationException("Token authentication requires a user ID.");
            token = Settings.AccessToken;
            userId = Settings.UserId;
        }
        http.DefaultRequestHeaders.Add("Authorization", AuthorizationHeader());
        http.DefaultRequestHeaders.Add("X-Emby-Authorization", AuthorizationHeader());

    }

    public static string AuthorizationHeader(string? accessToken = null) =>
        "MediaBrowser Client=\"Undertow\", Device=\"Emby\", DeviceId=\"undertow\", Version=\"0.45.6\"" +
        (string.IsNullOrEmpty(accessToken) ? "" : ", Token=\"" + accessToken + "\"");

    public static string Esc(string value) => Uri.EscapeDataString(value);

    private async Task Authenticate(CancellationToken ct, bool force = false)
    {
        await authGate.WaitAsync(ct);
        try
        {
            if (token != "" && !force) return;
            if (force && !string.IsNullOrEmpty(Settings.AccessToken))
                throw new InvalidOperationException("Saved upstream token expired; renew it in the private connection store.");
            using var response = await http.PostAsJsonAsync("Users/AuthenticateByName",
                new { Username = Settings.Username, Pw = Settings.Password }, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Upstream authentication failed: HTTP " + (int)response.StatusCode);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            token = doc.RootElement.GetProperty("AccessToken").GetString()!;
            userId = doc.RootElement.GetProperty("User").GetProperty("Id").GetString()!;
            Interlocked.Increment(ref Authentications);
        }
        finally { authGate.Release(); }
    }

    private async Task<JsonElement> Request(Func<string, string> path, CancellationToken ct, bool playback = false)
    {
        await Authenticate(ct);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(playback ? HttpMethod.Post : HttpMethod.Get, path(userId));
            request.Headers.Add("X-Emby-Token", token);
            // Standard Jellyfin uses Authorization; compatible servers may use
            // the legacy header. Both carry the same scoped server token.
            request.Headers.Add("Authorization", AuthorizationHeader(token));
            request.Headers.Add("X-Emby-Authorization", AuthorizationHeader(token));
            if (playback) request.Content = JsonContent.Create(new { UserId = userId, IsPlayback = true, AutoOpenLiveStream = true });
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            { await Authenticate(ct, true); continue; }
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Upstream returned HTTP " + (int)response.StatusCode);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.Clone();
        }
        throw new InvalidOperationException("Upstream authentication expired.");
    }

    private async Task<List<UpstreamItem>> Items(Func<string, string> path, CancellationToken ct)
    {
        Interlocked.Increment(ref MetadataRequests);
        var result = await Request(path, ct);
        return result.GetProperty("Items").Deserialize<List<UpstreamItem>>(JsonOptions) ?? new();
    }

    public Task<List<UpstreamItem>> Views(CancellationToken ct) => Items(u => "Users/" + Esc(u) + "/Views", ct);

    public async Task<CatalogPage> CatalogPage(string id, int start, CancellationToken ct)
    {
        Interlocked.Increment(ref MetadataRequests);
        var result = await Request(u => "Users/" + Esc(u) + "/Items?ParentId=" + Esc(id) + "&StartIndex=" + start + "&Limit=50&Fields=ProviderIds,Overview,Genres,RunTimeTicks", ct);
        return result.Deserialize<CatalogPage>(JsonOptions) ?? throw new InvalidOperationException("Invalid catalog response.");
    }

    public Task<List<UpstreamItem>> Seasons(string series, CancellationToken ct) => Items(u =>
        "Shows/" + Esc(series) + "/Seasons?UserId=" + Esc(u), ct);

    public Task<List<UpstreamItem>> Episodes(string series, string season, CancellationToken ct) => Items(u =>
        "Shows/" + Esc(series) + "/Episodes?UserId=" + Esc(u) + "&SeasonId=" + Esc(season), ct);

    public async Task<List<MediaSourceInfo>> Resolve(string id, CancellationToken ct)
    {
        Interlocked.Increment(ref PlaybackRequests);
        var result = await Request(u => "Items/" + Esc(id) + "/PlaybackInfo?UserId=" + Esc(u), ct, playback: true);
        result = NormalizePlaybackPaths(result, http.BaseAddress!, id, userId, token);
        var c = Plugin.Instance.Configuration;
        try
        {
            var sources = TranslateSources(result, c.MaximumVersions, c.Preferred4KSizeGb);
            PlaybackDiagnostics.Success(id);
            return sources;
        }
        catch (InvalidOperationException)
        {
            PlaybackDiagnostics.Miss(id, PlaybackDiagnostics.Reason(result));
            throw;
        }
    }

    public static JsonElement NormalizePlaybackPaths(JsonElement result, Uri server, string itemId, string userId, string accessToken)
    {
        var root = JsonNode.Parse(result.GetRawText())!;
        foreach (var source in root["MediaSources"]?.AsArray() ?? new JsonArray())
        {
            if (source == null || source["RequiresOpening"]?.GetValue<bool>() == true) continue;
            var path = source["Path"]?.GetValue<string>();
            if (Uri.TryCreate(path, UriKind.Absolute, out var direct) && direct.Scheme is "http" or "https") continue;
            var sourceId = source["Id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(sourceId)) continue;
            // File paths belong to the upstream, never to the Emby host. Ask the
            // upstream to serve that exact source through its authenticated API.
            source["Path"] = new Uri(server, "Videos/" + Esc(itemId) + "/stream?Static=true&MediaSourceId=" + Esc(sourceId) + "&UserId=" + Esc(userId)).AbsoluteUri;
            var headers = source["RequiredHttpHeaders"] as JsonObject ?? new JsonObject();
            headers["X-Emby-Token"] = accessToken;
            headers["Authorization"] = AuthorizationHeader(accessToken);
            source["RequiredHttpHeaders"] = headers;
        }
        return JsonSerializer.SerializeToElement(root);
    }

    public static List<MediaSourceInfo> TranslateSources(JsonElement result, int maximumVersions = 12, int preferred4KSizeGb = 20)
    {
        var sources = new List<MediaSourceInfo>();
        foreach (var raw in result.GetProperty("MediaSources").EnumerateArray())
        {
            if (raw.TryGetProperty("Type", out var type) && type.GetString() == "Placeholder") continue;
            var path = raw.TryGetProperty("Path", out var p) ? p.GetString() : null;
            if (!Uri.TryCreate(path, UriKind.Absolute, out var url) || (url.Scheme != "http" && url.Scheme != "https")) continue;
            var headers = raw.TryGetProperty("RequiredHttpHeaders", out var h)
                ? h.Deserialize<Dictionary<string, string>>(JsonOptions) ?? new() : new Dictionary<string, string>();
            // Session-based upstream sources are unsupported.
            if (raw.TryGetProperty("RequiresOpening", out var opening) && opening.ValueKind == JsonValueKind.True) continue;
            sources.Add(new MediaSourceInfo
            {
                Id = "jellyembifier-" + raw.GetProperty("Id").GetString(),
                Name = raw.TryGetProperty("Name", out var name) ? name.GetString() : "Upstream source",
                Path = path,
                Protocol = MediaProtocol.Http,
                IsRemote = true,
                Type = MediaSourceType.Default,
                Size = raw.TryGetProperty("Size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var bytes) ? bytes : null,
                Container = raw.TryGetProperty("Container", out var container) ? container.GetString() : null,
                RunTimeTicks = raw.TryGetProperty("RunTimeTicks", out var runtime) && runtime.ValueKind == JsonValueKind.Number && runtime.TryGetInt64(out var ticks) ? ticks : null,
                MediaStreams = raw.TryGetProperty("MediaStreams", out var streams)
                    ? streams.Deserialize<List<MediaStream>>(JsonOptions) ?? new() : new(),
                RequiredHttpHeaders = headers,
                SupportsDirectPlay = true,
                SupportsDirectStream = true,
                SupportsTranscoding = true
            });

        }
        if (sources.Count == 0) throw new InvalidOperationException("No playable direct HTTP source returned by upstream.");
        return sources.OrderByDescending(x => x.MediaStreams.Any(t => t.Type == MediaStreamType.Video && t.Width >= 3000))
            .ThenBy(x => x.MediaStreams.Any(t => t.Type == MediaStreamType.Video && t.Width >= 3000)
                ? Math.Abs((x.Size ?? long.MaxValue / 2) - Math.Clamp(preferred4KSizeGb, 1, 200) * 1_000_000_000L) : x.Size ?? long.MaxValue)
            .Take(Math.Clamp(maximumVersions, 1, 50)).ToList();
    }
}
