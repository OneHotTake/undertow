using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Services;

namespace Jellyembifier;

// The production portal reads this admin-only health endpoint.
[Route("/Jellyembifier/Status", "GET")]
public sealed class StatusRequest : IReturn<object> { }

[Authenticated(Roles = "Admin")]
public sealed class StatusService : IService
{
    public object Get(StatusRequest request) => new
    {
        Catalog = new { CatalogSync.Phase, CatalogSync.LastError, CatalogSync.Snapshot.Imported, CatalogSync.Snapshot.CatalogSeconds, CatalogSync.Snapshot.StructureSeconds, CatalogSync.Snapshot.ImportSeconds, SkipLibraryDuplicates = Plugin.Instance.Configuration.SkipLibraryDuplicates, SuppressedMovies = CatalogSync.Snapshot.SuppressedMovieIds.Count, SuppressedEpisodes = CatalogSync.Snapshot.SuppressedEpisodeIds.Count, SuppressedSeries = CatalogSync.Snapshot.SuppressedSeriesIds.Count, CatalogSync.RemovedDuplicates },
        Version = typeof(Plugin).Assembly.GetName().Version!.ToString(3),
        SourceOpenCalls = SelectedSourceProvider.OpenCalls,
        NativeProbeCalls = SelectedSourceProvider.ProbeCalls,
        NativeProbeFailures = SelectedSourceProvider.ProbeFailures
    };
}
