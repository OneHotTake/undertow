using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jellyembifier;

public sealed record UnavailablePlayback(string Title, DateTimeOffset CheckedAt, string Reason);

// Ten recent misses in memory. Never retain source paths, headers or raw notices.
public static class PlaybackDiagnostics
{
    private static readonly object Gate = new();
    private static readonly List<(string Id, UnavailablePlayback Entry)> Recent = new();
    public static IReadOnlyList<UnavailablePlayback> Snapshot()
    {
        lock (Gate) return Recent.Select(x => x.Entry).ToArray();
    }
    public static string Reason(JsonElement response)
    {
        var reasons = new List<string>();
        if (response.TryGetProperty("MediaSources", out var sources) && sources.ValueKind == JsonValueKind.Array)
            foreach (var source in sources.EnumerateArray())
            {
                if (!source.TryGetProperty("Type", out var type) || type.GetString() != "Placeholder" ||
                    !source.TryGetProperty("Name", out var name) || name.ValueKind != JsonValueKind.String) continue;
                // Parse only known filter counts and quality/resolution values.
                // Arbitrary upstream messages can contain private URLs or tokens.
                foreach (var line in (name.GetString() ?? "").Split('\n').Take(40))
                {
                    var category = Regex.Match(line, @"Excluded (Resolution|Quality|Language|Size|Codec|Cached|Cache|HDR) \((\d{1,6})\)");
                    if (category.Success) reasons.Add($"Excluded {category.Groups[1].Value.ToLowerInvariant()}: {category.Groups[2].Value}");
                    var detail = Regex.Match(line, @"^\s*•\s*(\d{1,6})× (Unknown|CAM|TS|TC|SCR|HDTS|HDCAM|DVDSCR|WEB-DL|WEBRip|BluRay|Remux|2160p|1080p|720p|480p|4K)\s*$");
                    if (detail.Success) reasons.Add($"{detail.Groups[1].Value} × {detail.Groups[2].Value}");
                }
            }
        return reasons.Count == 0 ? "Source returned no playable versions. Check source availability and filters." : string.Join(" · ", reasons.Distinct().Take(12));
    }
    public static void Miss(string id, string reason)
    {
        var item = Plugin.Instance == null ? null : CatalogSync.Snapshot.Items.Concat(CatalogSync.Snapshot.Children.Values.SelectMany(x => x)).FirstOrDefault(x => x.Id == id);
        var title = item?.Name ?? "Requested title";
        title = new string(title.Where(c => !char.IsControl(c)).Take(160).ToArray());
        lock (Gate)
        {
            Recent.RemoveAll(x => x.Id == id);
            Recent.Insert(0, (id, new(title, DateTimeOffset.UtcNow, reason)));
            if (Recent.Count > 10) Recent.RemoveRange(10, Recent.Count - 10);
        }
    }
    public static void Success(string id) { lock (Gate) Recent.RemoveAll(x => x.Id == id); }
}
