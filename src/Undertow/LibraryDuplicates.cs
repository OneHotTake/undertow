using MediaBrowser.Controller.Entities;
using MediaBrowser.Common;
using MediaBrowser.Controller.Channels;
using System.Text.Json;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyembifier;

// Provider IDs identify a film independently of its title, year or source item ID.
// A local series match suppresses the entire catalog series, regardless of episode coverage.
public sealed class LibraryMediaIndex
{
    private readonly HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> seriesIds = new(StringComparer.OrdinalIgnoreCase);
    public LibraryMediaIndex(IEnumerable<BaseItem> movies, IEnumerable<BaseItem>? series = null)
    {
        foreach (var movie in movies)
        {
            if (!IsLocalMovie(movie)) continue;
            foreach (var id in Keys(movie.ProviderIds)) ids.Add(id);
        }
        foreach (var show in series ?? Array.Empty<BaseItem>())
        {
            if (show is not MediaBrowser.Controller.Entities.TV.Series || !IsLocalFile(show)) continue;
            foreach (var id in Keys(show.ProviderIds)) seriesIds.Add(id);
        }
    }
    public static bool IsLocalMovie(BaseItem item) => item is MediaBrowser.Controller.Entities.Movies.Movie && IsLocalFile(item);
    public static bool IsLocalFile(BaseItem item) =>
        item.LocationType == LocationType.FileSystem && !item.IsVirtualItem && !item.IsPlaceHolder &&
        !string.IsNullOrWhiteSpace(item.Path) &&
        !(Uri.TryCreate(item.Path, UriKind.Absolute, out var uri) && !uri.IsFile) && !item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase);
    public bool Contains(UpstreamItem item) => item.Type == "Movie" && Keys(item.ProviderIds).Any(ids.Contains);
    public bool ContainsSeries(UpstreamItem series) => series.Type == "Series" && Keys(series.ProviderIds).Any(seriesIds.Contains);
    public void Apply(CatalogSnapshot snapshot)
    {
        snapshot.SuppressedMovieIds = snapshot.Items.Where(Contains).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var show in snapshot.Items.Where(ContainsSeries))
        {
            snapshot.SuppressedSeriesIds.Add(show.Id);
            if (!snapshot.Children.TryGetValue(show.Id, out var seasons)) continue;
            foreach (var season in seasons)
            {
                snapshot.SuppressedSeasonIds.Add(season.Id);
                if (snapshot.Children.TryGetValue(season.Id, out var episodes))
                    foreach (var episode in episodes) snapshot.SuppressedEpisodeIds.Add(episode.Id);
            }
        }
    }
    private static IEnumerable<string> Keys(IEnumerable<KeyValuePair<string, string>> providers)
    {
        foreach (var pair in providers)
        {
            // Ignore URLs and source-specific IDs; match only known movie namespaces.
            var provider = pair.Key.Trim().ToLowerInvariant();
            var value = pair.Value?.Trim();
            if (string.IsNullOrEmpty(value)) continue;
            if (provider == "imdb")
            {
                if (value.StartsWith("tt", StringComparison.OrdinalIgnoreCase) && value.Length > 2 && value[2..].All(char.IsAsciiDigit))
                    yield return provider + ":" + value.ToLowerInvariant();
            }
            else if (provider is "tmdb" or "tvdb" && long.TryParse(value, out var number) && number > 0)
                yield return provider + ":" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
    public static LibraryMediaIndex Load(ILibraryManager library, CancellationToken ct) => new(library.GetItemList(
        new InternalItemsQuery { Recursive = true, IncludeItemTypes = new[] { "Movie" }, HasPath = true, IsVirtualItem = false }, ct),
        library.GetItemList(new InternalItemsQuery { Recursive = true, IncludeItemTypes = new[] { "Series" } }, ct));
}

public static class LibraryDuplicateCleanup
{
    public static bool CanRemove(BaseItem item, CatalogSnapshot snapshot, LibraryMediaIndex local) =>
        item is MediaBrowser.Controller.Entities.Movies.Movie && string.IsNullOrWhiteSpace(item.Path) &&
        snapshot.Items.Any(row => row.Type == "Movie" && snapshot.SuppressedMovieIds.Contains(row.Id) &&
            JellyfinChannel.Encode(row.Id) == item.ExternalId && local.Contains(row));

    public static bool IsSuppressed(UpstreamItem item, CatalogSnapshot snapshot) => item.Type switch
    {
        "Movie" => snapshot.SuppressedMovieIds.Contains(item.Id),
        "Episode" => snapshot.SuppressedEpisodeIds.Contains(item.Id),
        "Series" => snapshot.SuppressedSeriesIds.Contains(item.Id),
        "Season" => snapshot.SuppressedSeasonIds.Contains(item.Id),
        _ => false
    };

    public static long[] Prepare(IApplicationHost host, CatalogSnapshot snapshot, CancellationToken ct)
    {
        var library = host.Resolve<ILibraryManager>();
        var channel = library.GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { "Channel" } }, ct)
            .OfType<Channel>().SingleOrDefault(x => x.Id == library.GetNewItemId("Channel " + JellyfinChannel.IdentityName, typeof(Channel)))
            ?? throw new InvalidOperationException("Channel missing.");
        // Query only this channel's descendants. A tag or provider match cannot authorize removing another library's item.
        var candidates = library.GetItemList(new InternalItemsQuery { ParentIds = new[] { channel.InternalId },
            Recursive = true, IncludeItemTypes = new[] { "Movie", "Series", "Season", "Episode" }, HasPath = false }, ct);
        var suppressed = snapshot.Items.Concat(snapshot.Children.Values.SelectMany(x => x))
            .Where(x => IsSuppressed(x, snapshot) || MovieAvailability.Hidden(x)).Select(x => JellyfinChannel.Encode(x.Id)).ToHashSet(StringComparer.Ordinal);
        var duplicates = candidates.Where(x => string.IsNullOrWhiteSpace(x.Path) && suppressed.Contains(x.ExternalId)).ToList();
        if (duplicates.Count == 0) return Array.Empty<long>();
        // Keep the complete catalog and a removal journal before changing native channel records.
        var directory = Path.Combine(Plugin.Instance.DataDirectory, "duplicate-backups", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonSerializer.Serialize(snapshot));
        File.WriteAllText(Path.Combine(directory, "removed.json"), JsonSerializer.Serialize(duplicates.Select(x => new { x.InternalId, x.ExternalId })));
        // Native user-data keys normally survive channel removal. Preserve a per-item receipt as well.
#pragma warning disable CS0618 // Pinned ABI compatibility; this bounded per-removal receipt needs every native user's data.
        var users = host.Resolve<IUserManager>().Users.ToArray();
#pragma warning restore CS0618
        File.WriteAllText(Path.Combine(directory, "user-data.json"), JsonSerializer.Serialize(duplicates.Select(item => new
        {
            item.ExternalId,
            Users = users.Select(user => new { user.InternalId, Data = BaseItem.UserDataManager.GetUserData(user, item) })
        })));
        return duplicates.Select(x => x.InternalId).ToArray();
    }
    public static int CountRemoved(IApplicationHost host, long[] previous, CancellationToken ct)
    {
        if (previous.Length == 0) return 0;
        var remaining = host.Resolve<ILibraryManager>().GetItemList(new InternalItemsQuery { ItemIds = previous }, ct)
            .Select(x => x.InternalId).ToHashSet();
        return previous.Count(x => !remaining.Contains(x));
    }
}
