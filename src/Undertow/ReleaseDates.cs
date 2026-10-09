using System.Net.Http.Headers;
using System.Text.Json;

namespace Jellyembifier;

// Separate from the upstream account; never return this credential to the UI.
public static class AvailabilityCredential
{
    private static string FileName => Path.Combine(Plugin.Instance.DataDirectory, "availability-credential.json");
    public static string Load() => File.Exists(FileName)
        ? JsonSerializer.Deserialize<string>(File.ReadAllText(FileName)) ?? "" : "";
    public static void Save(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        Directory.CreateDirectory(Plugin.Instance.DataDirectory);
        var temporary = FileName + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            JsonSerializer.Serialize(stream, value.Trim());
        }
        File.Move(temporary, FileName, true);
    }
}

public sealed record MovieReleaseDates(bool HasDates, DateTimeOffset? HomeRelease);

public static class ReleaseDates
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    public static string? TmdbId(UpstreamItem item)
    {
        var raw = item.ProviderIds.FirstOrDefault(x => x.Key.Equals("Tmdb", StringComparison.OrdinalIgnoreCase)).Value;
        return long.TryParse(raw, out var id) && id > 0 ? id.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
    }
    public static MovieReleaseDates Parse(JsonElement root)
    {
        var any = false;
        DateTimeOffset? home = null;
        foreach (var region in root.GetProperty("results").EnumerateArray())
            foreach (var release in region.GetProperty("release_dates").EnumerateArray())
            {
                if (!release.TryGetProperty("release_date", out var raw) ||
                    !DateTimeOffset.TryParse(raw.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var date)) continue;
                any = true;
                var type = release.GetProperty("type").GetInt32();
                // Any country: a home release establishes eligibility, not English audio or playable media.
                if (type is >= 4 and <= 6 && (home == null || date < home)) home = date;
            }
        return new(any, home);
    }
    public static async Task<MovieReleaseDates> Fetch(string id, string credential, CancellationToken ct)
    {
        // Numeric IDs only; neither user input nor an upstream URL chooses the destination.
        if (!long.TryParse(id, out var number) || number <= 0) throw new ArgumentException("Invalid TMDB movie ID.");
        var url = "https://api.themoviedb.org/3/movie/" + number + "/release_dates";
        using var request = new HttpRequestMessage(HttpMethod.Get, credential.Length > 80 ? url : url + "?api_key=" + Uri.EscapeDataString(credential));
        if (credential.Length > 80) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        using var response = await Http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Release metadata returned HTTP " + (int)response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return Parse(doc.RootElement);
    }
}
