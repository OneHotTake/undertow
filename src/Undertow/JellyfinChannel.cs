using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Drawing;

namespace Jellyembifier;

public sealed class JellyfinChannel : IChannel, IHasChannelFeatures
{
    public static int BrowseCalls;
    public static int ResolveCalls;
    // Emby hashes this identity into channel/item IDs. Visible branding is
    // applied to the native channel record separately by FolderArtwork.
    public const string IdentityName = "Jellyembifier — Movie & Series Test";
    public string Name => IdentityName;
    public string Description => "Jellyfin libraries in Emby, with playback sources loaded on demand.";
    public ChannelParentalRating ParentalRating => ChannelParentalRating.UsR;
    public ChannelFeatures Features => new()
    {
        ShowRootFoldersAtTopLevel = false,
        LibraryOptions = new LibraryOptions
        {
            EnableRealtimeMonitor = false,
            EnableMarkerDetection = false,
            EnableChapterImageExtraction = false,
            SaveLocalMetadata = false,
            AutomaticRefreshIntervalDays = 0,
            DownloadImagesInAdvance = true,
            TypeOptions = new[] { "Movie", "Series", "Season", "Episode" }.Select(type => new TypeOptions
            {
                Type = type,
                MetadataFetchers = new[] { "TheMovieDb" },
                MetadataFetcherOrder = new[] { "TheMovieDb" },
                ImageFetchers = new[] { "TheMovieDb" },
                ImageFetcherOrder = new[] { "TheMovieDb" }
            }).ToArray()
        }
    };
    public IEnumerable<ImageType> GetSupportedChannelImages() => new[] { ImageType.Primary };
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken ct) => Task.FromResult(new DynamicImageResponse
    { Stream = GetType().Assembly.GetManifestResourceStream("Jellyembifier.channel-card.png")!, Format = ImageFormat.Png });

    public Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        FolderArtwork.ApplyChannelName(Plugin.Instance.Host, ct);
        Interlocked.Increment(ref BrowseCalls);
        var snapshot = CatalogSync.Snapshot;
        if (snapshot.Completed == default)
            return Task.FromResult(new ChannelItemResult { Items = new(), TotalRecordCount = 0 });
        var cfg = JellyfinClient.Instance.Settings;
        var labels = Plugin.Instance.Configuration;
        var folder = Decode(query.FolderId);
        List<UpstreamItem> rows;
        if (folder == "") rows = RootFolders(cfg, labels);
        else if (folder == "anime") rows = AnimeFolders(labels);
        else if (folder == cfg.MovieLibraryId) rows = snapshot.Items.Where(x => x.Type == "Movie" && !IsAnime(x)).ToList();
        else if (folder == cfg.SeriesLibraryId) rows = snapshot.Items.Where(x => x.Type == "Series" && !IsAnime(x)).ToList();
        else if (folder is "anime:movies" or "anime:series") rows = snapshot.Items.Where(x => IsAnime(x) && x.Type == (folder == "anime:movies" ? "Movie" : "Series")).ToList();
        else rows = snapshot.Children.TryGetValue(folder, out var children) ? children : new();
        if (labels.SkipLibraryDuplicates)
            rows = rows.Where(x => !LibraryDuplicateCleanup.IsSuppressed(x, snapshot)).ToList();
        var mapped = rows.Select(Map).ToList();
        return Task.FromResult(new ChannelItemResult { TotalRecordCount = mapped.Count, Items = mapped.Skip(query.StartIndex ?? 0).Take(query.Limit ?? mapped.Count).ToList() });
    }

    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken ct)
    {
        var remoteId = Decode(id);
        var catalog = CatalogSync.Snapshot;
        if (catalog.Completed == default ||
            (!catalog.Items.Any(x => x.Id == remoteId && x.Type == "Movie") &&
             !catalog.Children.Values.SelectMany(x => x).Any(x => x.Id == remoteId && x.Type == "Episode")))
            throw new InvalidOperationException("Item is outside the synced catalog.");
        Interlocked.Increment(ref ResolveCalls);
        return await JellyfinClient.Instance.Resolve(remoteId, ct);
    }

    public static List<UpstreamItem> RootFolders(UpstreamSettings source, Configuration labels) => new()
    {
        new() { Id = source.MovieLibraryId, Name = labels.MoviesName, Type = "Folder" },
        new() { Id = source.SeriesLibraryId, Name = labels.SeriesName, Type = "Folder" },
        new() { Id = "anime", Name = labels.AnimeName, Type = "Folder" }
    };
    public static List<UpstreamItem> AnimeFolders(Configuration labels) => new()
    {
        new() { Id = "anime:movies", Name = labels.AnimeMoviesName, Type = "Folder" },
        new() { Id = "anime:series", Name = labels.AnimeSeriesName, Type = "Folder" }
    };

    public static bool IsAnime(UpstreamItem row)
    {
        var ids = row.ProviderIds.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return row.FromAnimeCatalog || new[] { "AniList", "Kitsu", "MyAnimeList", "AniDB", "MAL" }.Any(ids.Contains);
    }

    public static string Encode(string id) => "jf:" + id;
    public static string Decode(string? id) => string.IsNullOrEmpty(id) ? "" : id.StartsWith("jf:", StringComparison.Ordinal) ? id[3..] : throw new ArgumentException("Invalid channel ID.");

    public static ChannelItemInfo Map(UpstreamItem row)
    {
        var media = row.Type is "Movie" or "Episode";
        var item = new ChannelItemInfo
        {
            Id = Encode(row.Id), Name = row.Name, MediaType = ChannelMediaType.Video,
            Type = media ? ChannelItemType.Media : ChannelItemType.Folder,
            ForceUpdate = row.Type == "Folder",
            Overview = row.Overview, ProductionYear = row.ProductionYear, OfficialRating = row.OfficialRating,
            PremiereDate = row.PremiereDate, RunTimeTicks = row.RunTimeTicks, CommunityRating = row.CommunityRating,
            IndexNumber = row.IndexNumber, ParentIndexNumber = row.ParentIndexNumber, SeriesName = row.SeriesName,
            Genres = row.Genres, ProviderIds = new ProviderIdDictionary(row.ProviderIds),
            Tags = new() { "Undertow" }, MediaSources = new()
        };
        if (IsAnime(row)) item.Tags.Add("Anime");
        if (media)
        {
            item.ContentType = row.Type == "Movie" ? ChannelMediaContentType.Movie : ChannelMediaContentType.Episode;
        }
        else item.FolderType = row.Type switch
        { "Series" => ChannelFolderType.Series, "Season" => ChannelFolderType.Season, _ => ChannelFolderType.Container };
        return item;
    }
}
