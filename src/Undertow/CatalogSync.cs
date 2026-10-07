using System.Diagnostics;
using System.Text.Json;
using MediaBrowser.Common;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyembifier;

public sealed class CatalogSnapshot
{
    public DateTimeOffset Completed { get; set; }
    public DateTimeOffset Imported { get; set; }
    public List<UpstreamItem> Items { get; set; } = new();
    public Dictionary<string, List<UpstreamItem>> Children { get; set; } = new();
    public Dictionary<string, DateTimeOffset> SeriesChecked { get; set; } = new();
    public HashSet<string> SuppressedMovieIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> SuppressedEpisodeIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> SuppressedSeriesIds { get; set; } = new(StringComparer.Ordinal);
    public HashSet<string> SuppressedSeasonIds { get; set; } = new(StringComparer.Ordinal);
    public double CatalogSeconds { get; set; }
    public double StructureSeconds { get; set; }
    public double ImportSeconds { get; set; }
}

public static class CatalogSync
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static string Phase { get; private set; } = "Idle";
    public static string LastError { get; private set; } = "";
    public static int CatalogPages { get; private set; }
    public static int SeriesProcessed { get; private set; }
    public static int RemovedDuplicates { get; private set; }
    private static string PathName => Path.Combine(Plugin.Instance.DataDirectory, "catalog.json");
    private static CatalogSnapshot? snapshot;
    public static List<UpstreamItem> MergeAdditive(IEnumerable<UpstreamItem> previous, IEnumerable<UpstreamItem> incoming)
    {
        var rows = new Dictionary<string, UpstreamItem>(StringComparer.Ordinal);
        foreach (var item in incoming)
        {
            if (rows.TryGetValue(item.Id, out var duplicate)) duplicate.FromAnimeCatalog |= item.FromAnimeCatalog;
            else rows[item.Id] = item;
        }
        foreach (var old in previous)
        {
            if (rows.TryGetValue(old.Id, out var current)) current.FromAnimeCatalog |= old.FromAnimeCatalog;
            else rows[old.Id] = old;
        }
        return rows.Values.ToList();
    }
    public static CatalogSnapshot Snapshot => snapshot ??= Load();
    private static CatalogSnapshot Load()
    {
        if (!File.Exists(PathName)) return new();
        var value = JsonSerializer.Deserialize<CatalogSnapshot>(File.ReadAllText(PathName)) ?? new();
        if (value.Imported == default && value.ImportSeconds > 0) value.Imported = value.Completed.AddSeconds(value.ImportSeconds);
        return value;
    }
    private static void Save(CatalogSnapshot value)
    {
        Directory.CreateDirectory(Plugin.Instance.DataDirectory);
        File.WriteAllText(PathName + ".tmp", JsonSerializer.Serialize(value));
        File.Move(PathName + ".tmp", PathName, true);
        snapshot = value;
    }
    public static async Task Run(IApplicationHost host, CancellationToken ct, IProgress<double>? progress = null, bool rebuild = false)
    {
        if (!await Gate.WaitAsync(0, ct)) throw new InvalidOperationException("Catalog refresh already running.");
        try
        {
            LastError = ""; RemovedDuplicates = 0; CatalogPages = 0; SeriesProcessed = 0; Phase = "Catalog listing";
            var settings = Plugin.Instance.Configuration;
            var client = JellyfinClient.Instance;
            var views = await client.Views(ct);
            var chosen = views.Where(x => settings.CatalogIds.Length == 0 || settings.CatalogIds.Contains(x.Id)).ToList();
            if (chosen.Count == 0) throw new InvalidOperationException("No selected catalogs are available; previous snapshot retained.");
            var clock = Stopwatch.StartNew();
            var rows = new Dictionary<string, UpstreamItem>(StringComparer.Ordinal);
            foreach (var view in chosen)
            {
                var start = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var pageNumber = 0; ; pageNumber++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (pageNumber >= 10000) throw new InvalidOperationException("Catalog paging exceeded safety limit; previous snapshot retained.");
                    var page = await client.CatalogPage(view.Id, start, ct);
                    CatalogPages++;
                    if (page.Items.Count == 0)
                    {
                        if (page.TotalRecordCount > start) throw new InvalidOperationException("Incomplete catalog page; previous snapshot retained.");
                        break;
                    }
                    var fresh = 0;
                    foreach (var row in page.Items.Where(x => x.Type is "Movie" or "Series"))
                    {
                        if (seen.Add(row.Id)) fresh++;
                        row.FromAnimeCatalog = settings.AnimeCatalogIds.Contains(view.Id);
                        if (rows.TryGetValue(row.Id, out var previous)) previous.FromAnimeCatalog |= row.FromAnimeCatalog;
                        else rows[row.Id] = row;
                    }
                    if (fresh == 0) throw new InvalidOperationException("Catalog repeated a page; previous snapshot retained.");
                    start += page.Items.Count;
                    if (start >= page.TotalRecordCount) break;
                }
            }
            var previousSnapshot = Snapshot;
            // Catalogs can rotate or transiently return empty without signalling an error.
            // Membership disappearance never authorizes deletion in the additive sync.
            rows = MergeAdditive(previousSnapshot.Items, rows.Values).ToDictionary(x => x.Id, StringComparer.Ordinal);
            var next = new CatalogSnapshot { Items = rows.Values.ToList(), CatalogSeconds = clock.Elapsed.TotalSeconds,
                Children = new(previousSnapshot.Children), SeriesChecked = new(previousSnapshot.SeriesChecked) };
            Phase = "Season and episode metadata"; clock.Restart();
            var series = next.Items.Where(x => x.Type == "Series").ToList();
            foreach (var show in series)
            {
                ct.ThrowIfCancellationRequested();
                if (rebuild || !next.SeriesChecked.TryGetValue(show.Id, out var checkedAt) || DateTimeOffset.UtcNow - checkedAt >= TimeSpan.FromHours(settings.EffectiveSeriesRefreshHours))
                {
                    var seasons = await client.Seasons(show.Id, ct);
                    if (next.Children.TryGetValue(show.Id, out var oldSeasons)) seasons = MergeAdditive(oldSeasons, seasons);
                    var children = new Dictionary<string, List<UpstreamItem>>();
                    foreach (var season in seasons)
                    {
                        var episodes = await client.Episodes(show.Id, season.Id, ct);
                        children[season.Id] = next.Children.TryGetValue(season.Id, out var oldEpisodes) ? MergeAdditive(oldEpisodes, episodes) : episodes;
                    }
                    next.Children[show.Id] = seasons;
                    foreach (var pair in children) next.Children[pair.Key] = pair.Value;
                    next.SeriesChecked[show.Id] = DateTimeOffset.UtcNow;
                }
                SeriesProcessed++;
                progress?.Report(10 + 60.0 * SeriesProcessed / Math.Max(1, series.Count));
            }
            next.StructureSeconds = clock.Elapsed.TotalSeconds;
            if (settings.SkipLibraryDuplicates) LibraryMediaIndex.Load(host.Resolve<ILibraryManager>(), ct).Apply(next);
            // Atomic publish only after every upstream fetch has succeeded. Never persist sources.
            next.Completed = DateTimeOffset.UtcNow;
            Save(next);
            Phase = "Emby import and native metadata"; clock.Restart();
            var duplicateIds = settings.SkipLibraryDuplicates ? LibraryDuplicateCleanup.Prepare(host, next, ct) : Array.Empty<long>();
            var manager = host.Resolve<IChannelManager>();
            await manager.RefreshChannelContent(manager.GetChannel<JellyfinChannel>() ?? throw new InvalidOperationException("Channel missing."), 5, null!, ct);
            RemovedDuplicates = LibraryDuplicateCleanup.CountRemoved(host, duplicateIds, ct);
            await FolderArtwork.Apply(host, ct);
            next.ImportSeconds = clock.Elapsed.TotalSeconds; next.Imported = DateTimeOffset.UtcNow; Save(next);
            progress?.Report(100); Phase = "Idle";
        }
        catch (OperationCanceledException) { Phase = "Cancelled"; throw; }
        catch { LastError = "Refresh failed; check the scheduled task log. Native import or duplicate cleanup may be partial; retry the refresh."; Phase = "Failed"; throw; }
        finally { Gate.Release(); }
    }
}

public sealed class CatalogRefreshTask(IApplicationHost host) : IScheduledTask
{
    public static bool ForceNextRun;
    public static bool RebuildNextRun;
    public string Name => "Refresh Undertow catalogs";
    public string Key => "JellyembifierCatalogRefresh";
    public string Category => "Undertow";
    public string Description => "Metadata-only catalog sync and native Emby import; streams resolve only on playback.";
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[] { new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromHours(1).Ticks } };
    public static bool ShouldRun(bool enabled, bool force, DateTimeOffset imported, int hours, DateTimeOffset now) =>
        force || (enabled && now - imported >= TimeSpan.FromHours(Math.Clamp(hours, 1, 168)));
    public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
    {
        var c = Plugin.Instance.Configuration;
        var force = ForceNextRun; ForceNextRun = false;
        var rebuild = RebuildNextRun; RebuildNextRun = false;
        if (!ShouldRun(c.Enabled, force, CatalogSync.Snapshot.Imported, c.RefreshHours, DateTimeOffset.UtcNow)) return Task.CompletedTask;
        return CatalogSync.Run(host, cancellationToken, progress, rebuild);
    }
}
