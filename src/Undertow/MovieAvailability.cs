using System.Text.Json;
using MediaBrowser.Common;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;

namespace Jellyembifier;

public enum MovieReleaseRule { Ordinary, Upcoming, NeedsSources, MetadataUnknown }

public sealed class MovieAvailabilityEntry
{
    public string? TmdbId { get; set; }
    public DateTimeOffset MetadataChecked { get; set; }
    public DateTimeOffset? HomeRelease { get; set; }
    public bool HasReleaseDates { get; set; }
    public bool MetadataError { get; set; }
    public bool? Available { get; set; }
    public DateTimeOffset Checked { get; set; }
    public DateTimeOffset Attempted { get; set; }
    public bool SourceError { get; set; }
    public bool Published { get; set; }
}

public sealed class MovieAvailabilityState
{
    public Dictionary<string, MovieAvailabilityEntry> Movies { get; set; } = new(StringComparer.Ordinal);
    public List<DateTimeOffset> BackgroundAttempts { get; set; } = new();
    public bool PublicationPending { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

public static class MovieAvailabilityPolicy
{
    public const int DailyLimit = 48;
    public static bool IsRecent(UpstreamItem item, DateTimeOffset now) =>
        item.Type == "Movie" && item.PremiereDate is { } premiere && now - premiere < TimeSpan.FromDays(365);
    public static MovieReleaseRule Rule(UpstreamItem item, MovieAvailabilityEntry? entry, DateTimeOffset now)
    {
        if (!IsRecent(item, now)) return MovieReleaseRule.Ordinary;
        if (item.PremiereDate > now) return MovieReleaseRule.Upcoming;
        if (entry == null || entry.MetadataChecked == default || entry.TmdbId != ReleaseDates.TmdbId(item)) return MovieReleaseRule.MetadataUnknown;
        if (entry.HomeRelease <= now) return MovieReleaseRule.Ordinary;
        // A failed refresh retains earlier release evidence; it does not establish a new absence.
        if (entry.MetadataError && !entry.HasReleaseDates && entry.HomeRelease == null) return MovieReleaseRule.MetadataUnknown;
        return MovieReleaseRule.NeedsSources;
    }
    public static bool Hidden(UpstreamItem item, MovieAvailabilityEntry? entry, DateTimeOffset now) => Rule(item, entry, now) switch
    {
        MovieReleaseRule.Upcoming => true,
        MovieReleaseRule.NeedsSources => entry?.Available == false || (entry?.Available == null && entry?.Published != true),
        MovieReleaseRule.MetadataUnknown => entry?.Available != true && entry?.Published != true,
        _ => false
    };
    public static bool Due(MovieAvailabilityEntry entry, DateTimeOffset now) => now - entry.Attempted >= TimeSpan.FromHours(24);
    public static bool BudgetAllows(IEnumerable<DateTimeOffset> attempts, DateTimeOffset now)
    {
        var recent = attempts.Where(x => now - x < TimeSpan.FromHours(24)).ToArray();
        return recent.Length < DailyLimit && (recent.Length == 0 || now - recent.Max() >= TimeSpan.FromMinutes(1));
    }
    public static void Observe(MovieAvailabilityEntry entry, bool? available, DateTimeOffset now)
    {
        if (now < entry.Checked) return;
        if (now > entry.Attempted) entry.Attempted = now;
        entry.SourceError = available == null;
        if (available == null) return;
        entry.Available = available;
        entry.Checked = now;
    }
}

// Only IDs, dates and outcomes are durable. Media URLs, headers and credentials never enter this file.
public static class MovieAvailability
{
    private static readonly object Gate = new();
    private static MovieAvailabilityState? state;
    private static string FileName => Path.Combine(Plugin.Instance.DataDirectory, "movie-availability.json");
    private static MovieAvailabilityState State => state ??= File.Exists(FileName)
        ? JsonSerializer.Deserialize<MovieAvailabilityState>(File.ReadAllText(FileName)) ?? new() : new();
    private static void Save()
    {
        Directory.CreateDirectory(Plugin.Instance.DataDirectory);
        File.WriteAllText(FileName + ".tmp", JsonSerializer.Serialize(State));
        File.Move(FileName + ".tmp", FileName, true);
    }
    public static bool Hidden(UpstreamItem item)
    {
        if (Plugin.Instance == null || !Plugin.Instance.Configuration.CheckRecentMovieAvailability) return false;
        lock (Gate) return MovieAvailabilityPolicy.Hidden(item, State.Movies.GetValueOrDefault(item.Id), DateTimeOffset.UtcNow);
    }
    private static MovieAvailabilityEntry Entry(UpstreamItem row, bool published = false)
    {
        if (!State.Movies.TryGetValue(row.Id, out var entry))
            State.Movies[row.Id] = entry = new() { Published = published, TmdbId = ReleaseDates.TmdbId(row) };
        var tmdb = ReleaseDates.TmdbId(row);
        if (entry.TmdbId != tmdb)
            State.Movies[row.Id] = entry = new() { Published = entry.Published, TmdbId = tmdb };
        return entry;
    }
    private static bool NeedsMetadata(UpstreamItem row, MovieAvailabilityEntry entry, DateTimeOffset now) =>
        MovieAvailabilityPolicy.IsRecent(row, now) && row.PremiereDate <= now && entry.TmdbId != null &&
        !(entry.HomeRelease <= now) && now - entry.MetadataChecked >= TimeSpan.FromHours(24);
    private static void Changed(bool before, UpstreamItem row)
    {
        if (before == MovieAvailabilityPolicy.Hidden(row, State.Movies.GetValueOrDefault(row.Id), DateTimeOffset.UtcNow)) return;
        State.PublicationPending = true;
        State.Revision++;
    }
    private static async Task<bool> UpdateMetadata(UpstreamItem row, string credential, CancellationToken ct)
    {
        var id = ReleaseDates.TmdbId(row);
        if (id == null || credential == "") return false;
        MovieReleaseDates? dates = null;
        try { dates = await ReleaseDates.Fetch(id, credential, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* Retain prior evidence and expose the error without logging credential-bearing request URLs. */ }
        lock (Gate)
        {
            var entry = Entry(row);
            var before = MovieAvailabilityPolicy.Hidden(row, entry, DateTimeOffset.UtcNow);
            entry.MetadataChecked = DateTimeOffset.UtcNow;
            entry.MetadataError = dates == null;
            if (dates != null) { entry.HasReleaseDates = dates.HasDates; entry.HomeRelease = dates.HomeRelease; }
            Changed(before, row);
            Save();
        }
        return dates != null;
    }
    public static async Task RefreshMetadata(CatalogSnapshot snapshot, IApplicationHost host, CancellationToken ct)
    {
        if (!Plugin.Instance.Configuration.CheckRecentMovieAvailability) return;
        var library = host.Resolve<ILibraryManager>();
        var channel = library.GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { "Channel" } }, ct)
            .OfType<Channel>().SingleOrDefault(x => x.Id == library.GetNewItemId("Channel " + JellyfinChannel.IdentityName, typeof(Channel)));
        var channelId = channel?.InternalId ?? 0;
        var published = host.Resolve<ILibraryManager>().GetItemList(new InternalItemsQuery { ParentIds = new[] { channelId },
            Recursive = true, IncludeItemTypes = new[] { "Movie" }, HasPath = false }, ct).Select(x => x.ExternalId).ToHashSet();
        var rows = snapshot.Items.Where(x => MovieAvailabilityPolicy.IsRecent(x, DateTimeOffset.UtcNow) &&
            !snapshot.SuppressedMovieIds.Contains(x.Id)).ToArray();
        lock (Gate)
        {
            foreach (var row in rows) Entry(row, published.Contains(JellyfinChannel.Encode(row.Id)));
            Save();
        }
        var credential = AvailabilityCredential.Load();
        if (credential == "") return;
        // Metadata-only, bounded first fill. The minute task finishes any remainder without bursting indexers.
        var fetched = 0;
        var failures = 0;
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            bool due;
            lock (Gate) due = NeedsMetadata(row, Entry(row), DateTimeOffset.UtcNow);
            if (!due) continue;
            failures = await UpdateMetadata(row, credential, ct) ? 0 : failures + 1;
            if (++fetched >= 128) break;
            if (failures >= 3) break;
            await Task.Delay(200, ct);
        }
    }
    public static void Observe(string id, bool? available, DateTimeOffset? checkedAt = null)
    {
        if (Plugin.Instance == null || !Plugin.Instance.Configuration.CheckRecentMovieAvailability) return;
        var row = CatalogSync.Snapshot.Items.FirstOrDefault(x => x.Id == id && MovieAvailabilityPolicy.IsRecent(x, DateTimeOffset.UtcNow));
        if (row == null) return;
        lock (Gate)
        {
            var entry = Entry(row);
            var before = MovieAvailabilityPolicy.Hidden(row, entry, DateTimeOffset.UtcNow);
            MovieAvailabilityPolicy.Observe(entry, available, checkedAt ?? DateTimeOffset.UtcNow);
            Changed(before, row);
            Save();
        }
    }
    public static async Task CheckNext(CatalogSnapshot snapshot, CancellationToken ct)
    {
        if (!Plugin.Instance.Configuration.CheckRecentMovieAvailability || snapshot.Completed == default) return;
        var rows = snapshot.Items.Where(x => MovieAvailabilityPolicy.IsRecent(x, DateTimeOffset.UtcNow) &&
            !snapshot.SuppressedMovieIds.Contains(x.Id)).ToArray();
        var credential = AvailabilityCredential.Load();
        UpstreamItem? metadata;
        lock (Gate) metadata = rows.Where(x => NeedsMetadata(x, Entry(x), DateTimeOffset.UtcNow))
            .OrderBy(x => Entry(x).MetadataChecked).FirstOrDefault();
        if (metadata != null && credential != "") await UpdateMetadata(metadata, credential, ct);
        UpstreamItem? next;
        lock (Gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (!MovieAvailabilityPolicy.BudgetAllows(State.BackgroundAttempts, now)) return;
            next = rows.Where(x => MovieAvailabilityPolicy.Rule(x, Entry(x), now) == MovieReleaseRule.NeedsSources &&
                !Entry(x).MetadataError && MovieAvailabilityPolicy.Due(Entry(x), now)).OrderBy(x => Entry(x).Attempted).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (next == null) return;
            // Reserve before HTTP. Cancellation, failure, manual task reruns and restarts cannot reset the budgets.
            State.BackgroundAttempts.RemoveAll(x => now - x >= TimeSpan.FromHours(24));
            State.BackgroundAttempts.Add(now);
            Entry(next).Attempted = now;
            Save();
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { await JellyfinClient.Instance.Resolve(next.Id, timeout.Token); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (NoPlayableSourcesException) { /* Resolve already recorded the definitive empty result. */ }
        catch { Observe(next.Id, null); }
    }
    public static bool Pending { get { lock (Gate) return State.PublicationPending; } }
    public static long Revision { get { lock (Gate) return State.Revision; } }
    public static void Published(CatalogSnapshot snapshot, long revision)
    {
        lock (Gate)
        {
            foreach (var row in snapshot.Items.Where(x => State.Movies.ContainsKey(x.Id)))
                State.Movies[row.Id].Published = !snapshot.SuppressedMovieIds.Contains(row.Id) &&
                    (!Plugin.Instance.Configuration.CheckRecentMovieAvailability || !MovieAvailabilityPolicy.Hidden(row, State.Movies[row.Id], DateTimeOffset.UtcNow));
            if (State.Revision == revision) State.PublicationPending = false;
            State.PublishedAt = DateTimeOffset.UtcNow;
            Save();
        }
    }
    public static string Status(CatalogSnapshot snapshot)
    {
        if (!Plugin.Instance.Configuration.CheckRecentMovieAvailability) return "Recent-movie availability checks are off.";
        lock (Gate)
        {
            var now = DateTimeOffset.UtcNow;
            var movies = snapshot.Items.Where(x => x.Type == "Movie" && !snapshot.SuppressedMovieIds.Contains(x.Id)).ToArray();
            var rows = movies.Where(x => MovieAvailabilityPolicy.Rule(x, State.Movies.GetValueOrDefault(x.Id), now) == MovieReleaseRule.NeedsSources).ToArray();
            var attempts = State.BackgroundAttempts.Count(x => now - x < TimeSpan.FromHours(24));
            return $"{rows.Length} recent movies need source evidence; {rows.Count(x => State.Movies[x.Id].Available == true)} have candidates, {rows.Count(x => State.Movies[x.Id].Available == false)} returned none, {rows.Count(x => State.Movies[x.Id].Available == null)} await a result. " +
                $"{movies.Count(x => MovieAvailabilityPolicy.Rule(x, State.Movies.GetValueOrDefault(x.Id), now) == MovieReleaseRule.Upcoming)} upcoming movies wait for their premiere. " +
                $"{movies.Count(x => State.Movies.TryGetValue(x.Id, out var e) && (e.MetadataError || e.SourceError))} have lookup errors; previous decisions are retained. " +
                $"Background budget: {attempts}/{MovieAvailabilityPolicy.DailyLimit} in the last 24 hours. " +
                (AvailabilityCredential.Load() == "" ? "TMDB credential required. " : "") +
                (State.PublicationPending ? "An Emby publication update is pending." : State.PublishedAt == default ? "Save and refresh to apply the gate." : "Last completed Emby publication update: " + State.PublishedAt.ToLocalTime().ToString("MMM d, h:mm tt zzz") + ".");
        }
    }
}

public sealed class MovieAvailabilityTask(IApplicationHost host) : IScheduledTask
{
    public string Name => "Check Undertow recent-movie availability";
    public string Key => "UndertowMovieAvailability";
    public string Category => "Undertow";
    public string Description => "Opt-in, paced source discovery for recent movies without a confirmed home release. No video is opened or probed.";
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[] { new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerInterval, IntervalTicks = TimeSpan.FromMinutes(1).Ticks } };
    public Task Execute(CancellationToken cancellationToken, IProgress<double> progress) => CatalogSync.RunAvailability(host, cancellationToken);
}
