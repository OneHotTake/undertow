using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Services;

namespace Jellyembifier;

// Branch-only experiment: publish a bounded metadata fixture into ordinary libraries.
// The private marker is installed only in the isolated lab. No source URLs are stored.
public static class NativeLibraryLab
{
    public const string Marker = "UndertowNativeLibraryLab";
    public static bool Enabled => File.Exists(Path.Combine(Plugin.Instance.DataDirectory, "native-library-lab-enabled"));
    public static int ProviderCalls;
    public static int SourceLookups;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string? RemoteId(BaseItem item) => item.GetProviderId(Marker);
    public static bool Owns(BaseItem item) => Enabled && item is Movie or Episode &&
        string.IsNullOrEmpty(item.Path) && !string.IsNullOrEmpty(RemoteId(item));

    public static BaseItem Create(UpstreamItem row)
    {
        BaseItem item = row.Type switch
        {
            "Movie" => new Movie(), "Series" => new Series(),
            "Season" => new Season(), "Episode" => new Episode(),
            _ => throw new InvalidOperationException("Unsupported native fixture type.")
        };
        item.Name = row.Name; item.Overview = row.Overview; item.ProductionYear = row.ProductionYear;
        item.PremiereDate = row.PremiereDate; item.OfficialRating = row.OfficialRating;
        item.CommunityRating = row.CommunityRating; item.Genres = row.Genres.ToArray();
        item.IndexNumber = row.IndexNumber; item.ParentIndexNumber = row.ParentIndexNumber;
        item.RunTimeTicks = row.RunTimeTicks; item.Path = null; item.IsVirtualItem = false;
        item.ExternalId = JellyfinChannel.Encode(row.Id);
        foreach (var pair in row.ProviderIds) item.SetProviderId(pair.Key, pair.Value);
        item.SetProviderId(Marker, row.Id);
        return item;
    }

    public static LibraryOptions Options(string path, string type) => new()
    {
        ContentType = type, PathInfos = new[] { new MediaPathInfo { Path = path } },
        EnableRealtimeMonitor = false, SaveLocalMetadata = false, SaveSubtitlesWithMedia = false,
        EnableChapterImageExtraction = false, ExtractChapterImagesDuringLibraryScan = false,
        EnableMarkerDetection = false, EnableMarkerDetectionDuringLibraryScan = false,
        ThumbnailImagesIntervalSeconds = -1, AutoGenerateChapters = false,
        AutomaticRefreshIntervalDays = 0,
        TypeOptions = new[] { "Movie", "Series", "Season", "Episode" }.Select(t => new TypeOptions
        { Type = t, MetadataFetchers = Array.Empty<string>(), ImageFetchers = Array.Empty<string>() }).ToArray()
    };

    public static async Task<object> Publish(ILibraryManager library)
    {
        if (!Enabled) throw new InvalidOperationException("Native library experiment is disabled.");
        await Gate.WaitAsync();
        try
        {
            var snapshot = CatalogSync.Snapshot;
            if (snapshot.Items.Count(x => x.Type == "Movie") != 1 || snapshot.Items.Count(x => x.Type == "Series") != 1 ||
                snapshot.Children.Values.SelectMany(x => x).Count(x => x.Type == "Episode") > 3)
                throw new InvalidOperationException("Expected one movie, one series and at most three episodes.");
            var created = new List<BaseItem>();
            Folder Root(string type)
            {
                var path = Path.Combine(Plugin.Instance.DataDirectory, "native-library-lab", type);
                Directory.CreateDirectory(path);
                if (Directory.EnumerateFileSystemEntries(path).Any()) throw new InvalidOperationException("Anchor must remain empty.");
                var existing = library.GetVirtualFolders().SingleOrDefault(x => x.Locations.Contains(path));
                if (existing == null) library.AddVirtualFolder("Undertow Native Lab — " + (type == "movies" ? "Movies" : "TV"), type, Options(path, type), false);
                return library.GetItemList(new InternalItemsQuery { Path = path }).OfType<Folder>().SingleOrDefault()
                    ?? throw new InvalidOperationException("Native anchor missing.");
            }
            BaseItem Place(UpstreamItem row, Folder parent)
            {
                var item = Create(row);
                item.Id = library.GetNewItemId("UndertowNativeLab:" + row.Id, item.GetType());
                var existing = library.GetItemById(item.Id);
                if (existing != null)
                {
                    if (RemoteId(existing) != row.Id || existing.GetParent()?.InternalId != parent.InternalId)
                        throw new InvalidOperationException("Fixture identity or parent mismatch.");
                    created.Add(existing); return existing;
                }
                item.PresentationUniqueKey = item.Id.ToString("N") + "_";
                if (item is Season season && parent is Series series)
                { season.SeriesId = series.InternalId; season.SeriesName = series.Name; season.SeriesPresentationUniqueKey = series.PresentationUniqueKey; }
                if (item is Episode episode && parent is Season ps)
                { episode.SeriesId = ps.SeriesId; episode.SeriesName = ps.SeriesName; episode.SeriesPresentationUniqueKey = ps.SeriesPresentationUniqueKey; }
                parent.AddChild(item); created.Add(item); return item;
            }
            var movies = Root("movies"); var television = Root("tvshows");
            Place(snapshot.Items.Single(x => x.Type == "Movie"), movies);
            var showRow = snapshot.Items.Single(x => x.Type == "Series");
            var show = (Series)Place(showRow, television);
            foreach (var seasonRow in snapshot.Children[showRow.Id])
            {
                var season = (Season)Place(seasonRow, show);
                foreach (var episode in snapshot.Children[seasonRow.Id]) Place(episode, season);
            }
            return new { Items = created.Select(x => new { Id = x.InternalId.ToString(), x.Name, Type = x.GetType().Name, Pathless = string.IsNullOrEmpty(x.Path) }), ProviderCalls, SourceLookups };
        }
        finally { Gate.Release(); }
    }
}

[Route("/Undertow/NativeLibraryLab/Publish", "POST")]
public sealed class PublishNativeLibraryLab : IReturn<object> { }
[Route("/Undertow/NativeLibraryLab/Status", "GET")]
public sealed class NativeLibraryLabStatus : IReturn<object> { }
[Authenticated(Roles = "Admin")]
public sealed class NativeLibraryLabService(ILibraryManager library) : IService
{
    public Task<object> Post(PublishNativeLibraryLab request) => NativeLibraryLab.Publish(library);
    public object Get(NativeLibraryLabStatus request) => new
    {
        NativeLibraryLab.Enabled, NativeLibraryLab.ProviderCalls, NativeLibraryLab.SourceLookups,
        JellyfinClient.Instance.PlaybackRequests, SelectedSourceProvider.OpenCalls,
        SelectedSourceProvider.ProbeCalls, SelectedSourceProvider.ProbeFailures
    };
}
