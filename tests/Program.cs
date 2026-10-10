using System.Text.Json;
using Jellyembifier;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var movie = JellyfinChannel.Map(new() { Id = "movie", Type = "Movie", Name = "Movie", RunTimeTicks = 123, ProviderIds = new() { ["Imdb"] = "tt123" } });
Check(movie.Type == ChannelItemType.Media && movie.ContentType == ChannelMediaContentType.Movie && movie.RunTimeTicks == 123, "movie type and runtime");
Check(movie.MediaSources.Count == 0, "metadata mapping never supplies a resolved stream");
var series = JellyfinChannel.Map(new() { Id = "series", Type = "Series", Name = "Series" });
var season = JellyfinChannel.Map(new() { Id = "season", Type = "Season", Name = "Season", IndexNumber = 1 });
var episode = JellyfinChannel.Map(new() { Id = "episode", Type = "Episode", Name = "Pilot", IndexNumber = 1, ParentIndexNumber = 1 });
Check(series.FolderType == ChannelFolderType.Series && season.FolderType == ChannelFolderType.Season && season.IndexNumber == 1 && episode.ParentIndexNumber == 1, "native hierarchy and numbering");
Check(JellyfinChannel.Decode(movie.Id) == "movie" && JellyfinChannel.Encode("movie") == movie.Id, "stable upstream channel identity");
try { JellyfinChannel.Decode("foreign-id"); throw new Exception("Accepted foreign ID"); } catch (ArgumentException) { checks++; Console.WriteLine("PASS reject foreign channel identity"); }
using var response = JsonDocument.Parse("""
{"MediaSources":[
 {"Id":"notice","Type":"Placeholder","Path":"https://invalid.example/notice.mp4"},
 {"Id":"local","Type":"Default","Path":"/etc/passwd"},
 {"Id":"opening","Type":"Default","Path":"https://invalid.example/media.mkv","RequiresOpening":true},
 {"Id":"actual","Type":"Default","Path":"https://invalid.example/media.mkv","Container":null,"RunTimeTicks":null,"MediaStreams":[],"RequiredHttpHeaders":{"User-Agent":"fixture"}}
]}
""");
var sources = JellyfinClient.TranslateSources(response.RootElement);
Check(sources.Count == 1 && sources[0].RunTimeTicks == null, "null runtime and container accepted; notices, local paths and opening sessions rejected");
Check(sources[0].RequiredHttpHeaders["User-Agent"] == "fixture", "upstream required headers preserved");
using var empty = JsonDocument.Parse("{\"MediaSources\":[]}");
try { JellyfinClient.TranslateSources(empty.RootElement); throw new Exception("Accepted no source"); } catch (InvalidOperationException) { checks++; Console.WriteLine("PASS fail closed without playable sources"); }
var token = SelectedSourceProvider.MakeToken("jf:movie", "source");
Check(SelectedSourceProvider.ReadToken(token) == ("jf:movie", "source"), "selected source token binds exact item and version without a media URL");
var realChoices = new List<MediaBrowser.Model.Dto.MediaSourceInfo> {
    new() { Id = "first-real", Name = "First", Path = "https://example.invalid/first.mkv", RequiresOpening = true, OpenToken = SelectedSourceProvider.MakeToken("jf:movie", "first-real") },
    new() { Id = "second-real", Name = "Second", RequiresOpening = true, OpenToken = SelectedSourceProvider.MakeToken("jf:movie", "second-real") }
};
SelectedSourceProvider.AddAutomaticSourceAlias(realChoices, "mediasource_123");
Check(realChoices.Select(x => x.Id).SequenceEqual(new[] { "first-real", "second-real", "mediasource_123" }), "automatic alias retains all real version IDs and original ordering");
Check(realChoices[2].Name == "Automatic — First" && realChoices[0].Name == "First" && realChoices[2].Path == realChoices[0].Path && !realChoices[2].RequiresOpening && !realChoices[2].RequiresClosing && realChoices[2].LiveStreamId == null, "automatic descriptor is a separate finite file without a live-session identity");
Check(realChoices[2].OpenToken == null && SelectedSourceProvider.ReadToken(realChoices[0].OpenToken) == ("jf:movie", "first-real"), "automatic file preserves the offered URL while real versions retain exact opening tokens");
SelectedSourceProvider.AddAutomaticSourceAlias(realChoices, "mediasource_123");
Check(realChoices.Count == 3, "automatic alias cannot duplicate a source ID");
var absentChoices = new List<MediaBrowser.Model.Dto.MediaSourceInfo>();
SelectedSourceProvider.AddAutomaticSourceAlias(absentChoices, "mediasource_123");
Check(absentChoices.Count == 0, "no automatic choice when no real sources are offered");
var unopenedChoices = new List<MediaBrowser.Model.Dto.MediaSourceInfo> { new() { Id = "unmanaged" } };
SelectedSourceProvider.AddAutomaticSourceAlias(unopenedChoices, "mediasource_123");
Check(unopenedChoices.Count == 1, "automatic alias accepts only an already prepared real source");
var protectedChoices = new List<MediaBrowser.Model.Dto.MediaSourceInfo> { new(realChoices[0]) { RequiredHttpHeaders = new() { ["Authorization"] = "fixture" } } };
SelectedSourceProvider.AddAutomaticSourceAlias(protectedChoices, "mediasource_123");
Check(protectedChoices.Count == 1, "automatic direct file fails closed when HTTP headers are required");
var invalidChoices = new List<MediaBrowser.Model.Dto.MediaSourceInfo> { new(realChoices[0]) { Path = "/local/fixture.mkv" } };
SelectedSourceProvider.AddAutomaticSourceAlias(invalidChoices, "mediasource_123");
Check(invalidChoices.Count == 1, "automatic direct file cannot expose a non-HTTP path");
try { SelectedSourceProvider.ReadToken(SelectedSourceProvider.MakeToken("foreign", "source")); throw new Exception("Accepted foreign source token"); }
catch (ArgumentException) { checks++; Console.WriteLine("PASS selected-source token rejects foreign item"); }
using var twelve = JsonDocument.Parse(JsonSerializer.Serialize(new { MediaSources = Enumerable.Range(1, 15).Select(i => new { Id = i.ToString(), Path = "https://invalid.example/movie.mkv", Size = (long)i * 1_000_000_000, MediaStreams = new[] { new { Type = "Video", Index = 0, Width = 3840 } } }) }));
var ordered = JellyfinClient.TranslateSources(twelve.RootElement);
Check(ordered.Count == 12 && ordered[0].Size == 15_000_000_000L, "twelve-source cap and 4K size closest to 20 GB first");
var unprobed = new MediaBrowser.Model.Dto.MediaSourceInfo { DefaultAudioStreamIndex = 1, MediaStreams = new() {
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Video, Index = 0, Width = 3840 },
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Index = 1, Language = "rus" },
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Subtitle, Index = 2, Language = "eng" }
} };
SelectedSourceProvider.PrepareUnopenedSource(unprobed);
Check(unprobed.DefaultAudioStreamIndex == null && unprobed.MediaStreams.Count == 1 && unprobed.MediaStreams[0].Index == 0, "unprobed source has no guessed audio choice but retains video to prevent eager probing");
var probed = new MediaBrowser.Model.Dto.MediaSourceInfo { MediaStreams = new() {
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Index = 1, Language = "rus", IsDefault = true },
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Index = 7, Language = "eng", Title = "Original" },
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Index = 8, Language = "eng", Title = "Commentary" },
    new() { Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Index = 9, Language = "fra", Title = "Commentary" }
} };
SelectedSourceProvider.ExcludeDuplicateLanguageCommentary(probed);
Check(probed.MediaStreams.Select(t => t.Index).SequenceEqual(new[] {1,7,9}) && probed.MediaStreams[0].IsDefault, "omit duplicate-language commentary, retain original indices, file default and lone-language track");
var animeSettings = new UpstreamSettings();
Check(JellyfinChannel.Map(new() { Id = "anime", Name = "Anime", Type = "Folder" }).FolderType == ChannelFolderType.Container, "separate Anime container holds native movie and series types");
Check(JellyfinChannel.IsAnime(new() { ProviderIds = new() { ["Kitsu"] = "1" } }) &&
    JellyfinChannel.IsAnime(new() { ProviderIds = new() { ["Kitsu"] = "1", ["Tmdb"] = "2" } }) &&
    !JellyfinChannel.IsAnime(new() { Genres = new() { "Animation" } }), "upstream anime IDs take precedence over TMDB/TVDB and genre alone is insufficient");
Check(JellyfinChannel.Map(new() { Id = "anime-film", Type = "Movie", ProviderIds = new() { ["AniList"] = "5", ["Tvdb"] = "10" } }).Tags.Contains("Anime"), "anime provenance survives native metadata enrichment as an Anime tag");
var catalogAnime = new UpstreamItem { Id = "catalog-film", Type = "Movie", ProviderIds = new() { ["Tmdb"] = "129" } };
var regularFilm = new UpstreamItem { Id = "regular", Type = "Movie" };
catalogAnime.FromAnimeCatalog = true;
Check(JellyfinChannel.IsAnime(catalogAnime) && !JellyfinChannel.IsAnime(regularFilm), "catalog origin classifies mainstream-ID-only anime without classifying unrelated titles");
Check(JellyfinChannel.Map(catalogAnime).ContentType == ChannelMediaContentType.Movie && JellyfinChannel.Map(catalogAnime).Tags.Contains("Anime"), "catalog provenance preserves native movie type and Anime tag");
var retained = new UpstreamItem { Id = "old", Type = "Movie", FromAnimeCatalog = true };
var updated = new UpstreamItem { Id = "old", Type = "Movie", Name = "Updated" };
Check(CatalogSync.MergeAdditive(new[] { retained }, Array.Empty<UpstreamItem>()).Single().Id == "old", "empty upstream membership does not purge an existing title");
var merged = CatalogSync.MergeAdditive(new[] { retained }, new[] { updated, new UpstreamItem { Id = "new" } });
Check(merged.Count == 2 && merged.Single(x => x.Id == "old").Name == "Updated" && merged.Single(x => x.Id == "old").FromAnimeCatalog, "additive updates preserve anime provenance and add new identities");
Check(JsonSerializer.Deserialize<UpstreamItem>(JsonSerializer.Serialize(retained))!.FromAnimeCatalog, "catalog-origin classification persists across snapshot restart");
var labels = new Configuration { MoviesName = "Filme", SeriesName = "Serien", AnimeName = "アニメ", AnimeMoviesName = "アニメ映画", AnimeSeriesName = "アニメシリーズ" };
var englishFolders = JellyfinChannel.RootFolders(animeSettings, new()).Concat(JellyfinChannel.AnimeFolders(new())).ToList();
var translatedFolders = JellyfinChannel.RootFolders(animeSettings, labels).Concat(JellyfinChannel.AnimeFolders(labels)).ToList();
Check(translatedFolders.Select(x => x.Id).SequenceEqual(englishFolders.Select(x => x.Id)) && translatedFolders.Select(x => x.Name).SequenceEqual(new[] { "Filme", "Serien", "アニメ", "アニメ映画", "アニメシリーズ" }), "five localized folder labels preserve internal identities");
Check(JellyfinClient.TranslateSources(twelve.RootElement, maximumVersions: 3, preferred4KSizeGb: 5).Count == 3 && JellyfinClient.TranslateSources(twelve.RootElement, 3, 5)[0].Size == 5_000_000_000L, "settings control version cap and preferred 4K size");
Check(translatedFolders.Select(x => FolderArtwork.AssetFor(JellyfinChannel.Encode(x.Id), animeSettings)).SequenceEqual(new[] { "movies", "series", "anime", "anime-movies", "anime-series" }) && FolderArtwork.AssetFor("jf:foreign", animeSettings) == null, "embedded artwork follows all five stable folder IDs and rejects other items");
Check(typeof(Plugin).Assembly.GetManifestResourceNames().Count(x => x.StartsWith("Jellyembifier.Assets.") && x.EndsWith(".png")) == 5, "five folder images ship inside the plugin assembly");
using var nativeNumbers = JsonDocument.Parse("{\"number\":6,\"input\":\"7\"}");
Check(Jellyembifier.UI.SettingsValues.Integer(nativeNumbers.RootElement, "number") == 6 && Jellyembifier.UI.SettingsValues.Integer(nativeNumbers.RootElement, "input") == 7, "native settings accept both numeric inputs submitted as strings and API JSON numbers");
var duplicates = CatalogSync.MergeAdditive(Array.Empty<UpstreamItem>(), new[] { new UpstreamItem { Id = "episode", Name = "First" }, new UpstreamItem { Id = "episode", Name = "Duplicate", FromAnimeCatalog = true } });
Check(duplicates.Count == 1 && duplicates[0].Name == "First" && duplicates[0].FromAnimeCatalog, "duplicate upstream episode IDs merge deterministically without aborting sync or losing anime provenance");
using var remoteFiles = JsonDocument.Parse("{\"MediaSources\":[{\"Id\":\"disk\",\"Path\":\"/remote/movie.mkv\",\"RequiredHttpHeaders\":{\"Test\":\"keep\"}},{\"Id\":\"cdn\",\"Path\":\"https://cdn.example/movie.mkv\"},{\"Id\":\"session\",\"Path\":\"/remote/session\",\"RequiresOpening\":true}]}");
var normalized = JellyfinClient.NormalizePlaybackPaths(remoteFiles.RootElement, new Uri("https://server.example/jellyfin/"), "movie", "user", "fake-token").GetProperty("MediaSources");
Check(normalized[0].GetProperty("Path").GetString() == "https://server.example/jellyfin/Videos/movie/stream?Static=true&MediaSourceId=disk&UserId=user" && normalized[0].GetProperty("RequiredHttpHeaders").GetProperty("X-Emby-Token").GetString() == "fake-token" && normalized[0].GetProperty("RequiredHttpHeaders").GetProperty("Test").GetString() == "keep", "remote filesystem paths become authenticated exact-source upstream streams preserving base path and headers");
Check(normalized[0].GetProperty("RequiredHttpHeaders").GetProperty("Authorization").GetString() == JellyfinClient.AuthorizationHeader("fake-token") && !JellyfinClient.AuthorizationHeader().Contains("Token="), "standard Jellyfin stream authorization carries the scoped token; login identifies the client without a token");
Check(normalized[1].GetProperty("Path").GetString() == "https://cdn.example/movie.mkv" && !normalized[1].TryGetProperty("RequiredHttpHeaders", out _) && normalized[2].GetProperty("Path").GetString() == "/remote/session", "direct CDN URLs receive no server token and session-opening sources remain unsupported");
var now = DateTimeOffset.UtcNow;
Check(!CatalogRefreshTask.ShouldRun(false, false, default, 6, now), "disabled schedule skips automatic sync");
Check(CatalogRefreshTask.ShouldRun(false, true, now, 6, now), "manual refresh runs with schedule disabled");
Check(!CatalogRefreshTask.ShouldRun(true, false, now.AddHours(-5), 6, now) && CatalogRefreshTask.ShouldRun(true, false, now.AddHours(-6), 6, now), "scheduled sync respects due interval");

using (var notices = JsonDocument.Parse("{\"MediaSources\":[{\"Type\":\"Placeholder\",\"Name\":\"Removal Reasons\\nExcluded Quality (6)\\n    • 4× CAM\\n    • 2× TS\\nhttps://private.example/token-secret\"}]}"))
{
    var reason = PlaybackDiagnostics.Reason(notices.RootElement);
    Check(reason.Contains("Excluded quality: 6") && reason.Contains("4 × CAM") && reason.Contains("2 × TS") && !reason.Contains("private") && !reason.Contains("token"), "placeholder diagnostics retain filter counts without private upstream text");
}
using (var notices = JsonDocument.Parse("{\"MediaSources\":[{\"Type\":\"Placeholder\",\"Name\":\"private-password-only\"}]}"))
    Check(!PlaybackDiagnostics.Reason(notices.RootElement).Contains("private-password"), "unknown upstream notices use a safe fallback");
for (var i = 0; i < 12; i++) PlaybackDiagnostics.Miss("diagnostic-test-" + i, "No playable versions.");
Check(PlaybackDiagnostics.Snapshot().Count == 10, "recent misses are bounded to ten in memory");
PlaybackDiagnostics.Success("diagnostic-test-11");
Check(PlaybackDiagnostics.Snapshot().Count == 9, "successful lookup clears that title's stale miss");


Check(new Configuration().EffectiveSeriesRefreshHours == 6 && new Configuration { SeriesRefreshDays = 7 }.EffectiveSeriesRefreshHours == 168 && new Configuration { SeriesRefreshDays = 7, SeriesRefreshHours = 6 }.EffectiveSeriesRefreshHours == 6, "six-hour structure default; legacy day interval preserved until explicitly changed");
var configSerializer = new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
using var legacyXml = new StringReader("<Configuration><SeriesRefreshDays>7</SeriesRefreshDays></Configuration>");
var legacyConfig = (Configuration)configSerializer.Deserialize(legacyXml)!;
using var upgradedXml = new StringWriter();
configSerializer.Serialize(upgradedXml, legacyConfig);
using var rereadXml = new StringReader(upgradedXml.ToString());
Check(((Configuration)configSerializer.Deserialize(rereadXml)!).EffectiveSeriesRefreshHours == 168 && !upgradedXml.ToString().Contains("<SeriesRefreshDays>"), "legacy day settings survive XML save/reload as hours without retaining the retired field");

var ownedMovie = new MediaBrowser.Controller.Entities.Movies.Movie {
    Path = "/media/movies/Shawshank.mkv", ProviderIds = new MediaBrowser.Model.Entities.ProviderIdDictionary(new Dictionary<string, string> { ["Imdb"] = "tt0111161", ["Tmdb"] = "278" }) };
var ownedIndex = new LibraryMediaIndex(new[] { ownedMovie });
var duplicateMovie = new UpstreamItem { Id = "shawshank", Type = "Movie", Name = "Different title", ProviderIds = new() { ["IMDB"] = " TT0111161 " } };
Check(ownedIndex.Contains(duplicateMovie), "movie provider IDs match despite title, key casing and surrounding whitespace");
Check(ownedIndex.Contains(new() { Type = "Movie", ProviderIds = new() { ["Tmdb"] = "000278" } }), "numeric movie IDs normalize leading zeroes");
Check(!ownedIndex.Contains(new() { Type = "Movie", Name = "The Shawshank Redemption" }) && !ownedIndex.Contains(new() { Type = "Movie", ProviderIds = new() { ["Tmdb"] = "279" } }), "same title without matching IDs and different provider IDs do not suppress movies");
Check(!ownedIndex.Contains(new() { Type = "Series", ProviderIds = new() { ["Tmdb"] = "278" } }), "movie match cannot suppress a series with the same numeric ID");
var streamMovie = new MediaBrowser.Controller.Entities.Movies.Movie { Path = "/media/movie.STRM", ProviderIds = ownedMovie.ProviderIds };
var virtualMovie = new MediaBrowser.Controller.Entities.Movies.Movie { ProviderIds = ownedMovie.ProviderIds };
var urlMovie = new MediaBrowser.Controller.Entities.Movies.Movie { Path = "https://example.invalid/movie.mkv", ProviderIds = ownedMovie.ProviderIds };
Check(!new LibraryMediaIndex(new[] { streamMovie, virtualMovie, urlMovie }).Contains(duplicateMovie), "stream files, pathless channel movies and remote URLs never count as local copies");
Check(!new LibraryMediaIndex(new[] { new MediaBrowser.Controller.Entities.Movies.Movie { Path = "/media/movie.mkv", ProviderIds = new MediaBrowser.Model.Entities.ProviderIdDictionary(new Dictionary<string,string> { ["Official Website"] = "same", ["Tmdb"] = "0", ["Imdb"] = "invalid" }) } }).Contains(new() { Type = "Movie", ProviderIds = new() { ["Official Website"] = "same", ["Tmdb"] = "0", ["Imdb"] = "invalid" } }), "unknown namespaces and invalid provider IDs cannot establish ownership");
var duplicateSnapshot = new CatalogSnapshot { Items = new() { duplicateMovie }, SuppressedMovieIds = new() { "shawshank" } };
var importedDuplicate = new MediaBrowser.Controller.Entities.Movies.Movie { ExternalId = "jf:shawshank" };
Check(LibraryDuplicateCleanup.CanRemove(importedDuplicate, duplicateSnapshot, ownedIndex) && !LibraryDuplicateCleanup.CanRemove(ownedMovie, duplicateSnapshot, ownedIndex), "cleanup accepts only a pathless matched Undertow identity and never a local file");
Check(!LibraryDuplicateCleanup.CanRemove(new MediaBrowser.Controller.Entities.Movies.Movie { ExternalId = "jf:other" }, duplicateSnapshot, ownedIndex) && !LibraryDuplicateCleanup.CanRemove(importedDuplicate, duplicateSnapshot, new LibraryMediaIndex(Array.Empty<MediaBrowser.Controller.Entities.BaseItem>())), "cleanup revalidates ownership and rejects unrelated imported identities");
var savedDuplicates = JsonSerializer.Deserialize<CatalogSnapshot>(JsonSerializer.Serialize(duplicateSnapshot))!;
Check(savedDuplicates.Items.Count == 1 && savedDuplicates.SuppressedMovieIds.Contains("shawshank"), "suppression survives restart without deleting retained catalog metadata");
Check(!new Configuration().SkipLibraryDuplicates && !legacyConfig.SkipLibraryDuplicates, "new and older installs keep duplicate cleanup off until explicitly enabled");
using var switchXml = new StringWriter();
configSerializer.Serialize(switchXml, new Configuration { SkipLibraryDuplicates = true });
using var switchReader = new StringReader(switchXml.ToString());
Check(((Configuration)configSerializer.Deserialize(switchReader)!).SkipLibraryDuplicates, "duplicate switch survives native configuration XML save and reload");

var ownedShow = new MediaBrowser.Controller.Entities.TV.Series { Path = "/media/tv/Example", InternalId = 10,
    ProviderIds = new MediaBrowser.Model.Entities.ProviderIdDictionary(new Dictionary<string,string> { ["Tvdb"] = "81189", ["Imdb"] = "tt0903747" }) };
var mediaIndex = new LibraryMediaIndex(new[] { ownedMovie }, new[] { ownedShow });
var upstreamShow = new UpstreamItem { Id = "show", Type = "Series", ProviderIds = new() { ["TVDB"] = "81189" } };
Check(mediaIndex.ContainsSeries(upstreamShow) && !mediaIndex.ContainsSeries(new() { Type = "Series", ProviderIds = new() { ["Tvdb"] = "81190" } }), "series dedup matches provider IDs without movie namespace collisions");
Check(!mediaIndex.ContainsSeries(new() { Type = "Movie", ProviderIds = new() { ["Tvdb"] = "81189" } }) && !mediaIndex.Contains(new() { Type = "Movie", ProviderIds = new() { ["Tvdb"] = "81189" } }), "owned series cannot suppress a movie with the same numeric provider ID");
var showSnapshot = new CatalogSnapshot { Items = new() { upstreamShow }, Children = new() {
    ["show"] = new() { new() { Id = "season", Type = "Season", IndexNumber = 1 } },
    ["season"] = new() { new() { Id = "ep1", Type = "Episode", IndexNumber = 1 }, new() { Id = "ep2", Type = "Episode", IndexNumber = 2 } } } };
mediaIndex.Apply(showSnapshot);
Check(showSnapshot.SuppressedSeriesIds.Contains("show") && showSnapshot.SuppressedSeasonIds.Contains("season") && showSnapshot.SuppressedEpisodeIds.SetEquals(new[] { "ep1", "ep2" }), "local series suppresses the entire upstream hierarchy without checking episode coverage");
Check(LibraryDuplicateCleanup.IsSuppressed(upstreamShow, showSnapshot) && LibraryDuplicateCleanup.IsSuppressed(showSnapshot.Children["season"][0], showSnapshot), "series and episode browse filtering use the same whole-series suppression");
Check(showSnapshot.Items.Count == 1 && showSnapshot.Children["season"].Count == 2, "whole-series suppression retains all source metadata for reversible republication");
var emptyShow = new CatalogSnapshot { Items = new() { upstreamShow } }; mediaIndex.Apply(emptyShow);
Check(emptyShow.SuppressedSeriesIds.Contains("show"), "a series ID match does not depend on cached seasons or full episode ownership");
Check(!new LibraryMediaIndex(Array.Empty<MediaBrowser.Controller.Entities.BaseItem>(), new[] { new MediaBrowser.Controller.Entities.TV.Series { ProviderIds = ownedShow.ProviderIds } }).ContainsSeries(upstreamShow), "pathless Undertow series never counts as local ownership");
var availabilityNow = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
var recentMovie = new UpstreamItem { Id = "recent", Type = "Movie", PremiereDate = availabilityNow.AddDays(-30), ProviderIds = new() { ["Tmdb"] = "123" } };
var recentEntry = new MovieAvailabilityEntry { TmdbId = "123", MetadataChecked = availabilityNow, HasReleaseDates = true };
Check(MovieAvailabilityPolicy.Rule(recentMovie, recentEntry, availabilityNow) == MovieReleaseRule.NeedsSources && MovieAvailabilityPolicy.Hidden(recentMovie, recentEntry, availabilityNow), "recent movie without home-release evidence waits for a source result");
Check(!MovieAvailabilityPolicy.Hidden(recentMovie, new() { TmdbId = "123", MetadataChecked = availabilityNow, Published = true }, availabilityNow), "an existing movie remains visible until its first conclusive source result");
MovieAvailabilityPolicy.Observe(recentEntry, true, availabilityNow);
Check(!MovieAvailabilityPolicy.Hidden(recentMovie, recentEntry, availabilityNow), "any accepted source publishes an eligible movie without probing media");
MovieAvailabilityPolicy.Observe(recentEntry, null, availabilityNow.AddHours(1));
Check(recentEntry.Available == true && recentEntry.SourceError && !MovieAvailabilityPolicy.Hidden(recentMovie, recentEntry, availabilityNow), "transport failure retains an available movie rather than treating it as empty");
MovieAvailabilityPolicy.Observe(recentEntry, false, availabilityNow.AddDays(1));
Check(MovieAvailabilityPolicy.Hidden(recentMovie, recentEntry, availabilityNow.AddDays(1)), "definitive empty response hides a previously available movie");
MovieAvailabilityPolicy.Observe(recentEntry, null, availabilityNow.AddDays(2));
Check(recentEntry.Available == false && recentEntry.SourceError, "transport failure retains a previous empty decision without manufacturing a source");
MovieAvailabilityPolicy.Observe(recentEntry, true, availabilityNow.AddDays(3));
Check(!MovieAvailabilityPolicy.Hidden(recentMovie, recentEntry, availabilityNow.AddDays(3)), "a later positive lookup republishes retained metadata");
MovieAvailabilityPolicy.Observe(recentEntry, false, availabilityNow.AddDays(2));
Check(recentEntry.Available == true, "older cached response cannot overwrite a newer source observation");
var upcomingMovie = new UpstreamItem { Type = "Movie", PremiereDate = availabilityNow.AddDays(2) };
Check(MovieAvailabilityPolicy.Rule(upcomingMovie, recentEntry, availabilityNow) == MovieReleaseRule.Upcoming && MovieAvailabilityPolicy.Hidden(upcomingMovie, recentEntry, availabilityNow), "future premiere waits without a scheduled source search");
Check(MovieAvailabilityPolicy.Rule(new() { Type = "Movie", PremiereDate = availabilityNow.AddDays(-365) }, null, availabilityNow) == MovieReleaseRule.Ordinary &&
    MovieAvailabilityPolicy.Rule(new() { Type = "Series", PremiereDate = availabilityNow }, null, availabilityNow) == MovieReleaseRule.Ordinary &&
    MovieAvailabilityPolicy.Rule(new() { Type = "Movie" }, null, availabilityNow) == MovieReleaseRule.Ordinary, "older movies, series and movies without a usable premiere date are outside the gate");
Check(!MovieAvailabilityPolicy.Hidden(recentMovie, new() { Published = true, MetadataError = true }, availabilityNow) &&
    MovieAvailabilityPolicy.Hidden(recentMovie, null, availabilityNow), "unknown release metadata retains an existing entry while withholding an unverified new title");
var homeEntry = new MovieAvailabilityEntry { TmdbId = "123", MetadataChecked = availabilityNow, HomeRelease = availabilityNow.AddDays(-1) };
Check(MovieAvailabilityPolicy.Rule(recentMovie, homeEntry, availabilityNow) == MovieReleaseRule.Ordinary, "past home release leaves the special source-check queue without claiming playback success");
homeEntry.TmdbId = "124";
Check(MovieAvailabilityPolicy.Rule(recentMovie, homeEntry, availabilityNow) == MovieReleaseRule.MetadataUnknown, "release evidence cannot cross a changed TMDB identity");
using (var releases = JsonDocument.Parse("""{"results":[{"iso_3166_1":"US","release_dates":[{"type":3,"release_date":"2026-09-01T00:00:00Z"},{"type":4,"release_date":"2026-11-01T00:00:00Z"}]},{"iso_3166_1":"GB","release_dates":[{"type":5,"release_date":"2026-10-01T00:00:00Z"},{"type":6,"release_date":"2026-10-02T00:00:00Z"}]}]}"""))
{
    var parsedDates = ReleaseDates.Parse(releases.RootElement);
    Check(parsedDates.HasDates && parsedDates.HomeRelease == new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "release parsing accepts digital, physical and TV across countries without confusing theatrical dates");
}
Check(!MovieAvailabilityPolicy.BudgetAllows(new[] { availabilityNow.AddSeconds(-59) }, availabilityNow) &&
    MovieAvailabilityPolicy.BudgetAllows(new[] { availabilityNow.AddMinutes(-1) }, availabilityNow), "persisted global budget prevents more than one background source attempt per minute");
var fullBudget = Enumerable.Range(0, MovieAvailabilityPolicy.DailyLimit).Select(i => availabilityNow.AddMinutes(-i - 2)).ToList();
Check(!MovieAvailabilityPolicy.BudgetAllows(fullBudget, availabilityNow) && MovieAvailabilityPolicy.BudgetAllows(fullBudget, availabilityNow.AddHours(24)), "48-attempt ceiling uses a rolling 24-hour window");
Check(!MovieAvailabilityPolicy.Due(new() { Attempted = availabilityNow.AddHours(-23) }, availabilityNow) &&
    MovieAvailabilityPolicy.Due(new() { Attempted = availabilityNow.AddHours(-24) }, availabilityNow), "per-movie daily cooldown includes reserved failed/cancelled attempts");
var durableAvailability = JsonSerializer.Deserialize<MovieAvailabilityState>(JsonSerializer.Serialize(new MovieAvailabilityState { Movies = new() { ["recent"] = recentEntry }, BackgroundAttempts = fullBudget }))!;
Check(durableAvailability.Movies["recent"].Available == true && !MovieAvailabilityPolicy.BudgetAllows(durableAvailability.BackgroundAttempts, availabilityNow), "restart preserves positive evidence and consumed search allowance");
Check(!new Configuration().CheckRecentMovieAvailability && !legacyConfig.CheckRecentMovieAvailability, "new and existing installs keep background source discovery off until opted in");
var cacheNow = availabilityNow;
var sourceCache = new SourceLookupCache(() => cacheNow);
var fetches = 0;
async Task<JsonElement> FakeSources(CancellationToken ct) { Interlocked.Increment(ref fetches); await Task.Delay(10, ct); return response.RootElement; }
await Task.WhenAll(sourceCache.Get("same", true, FakeSources, CancellationToken.None), sourceCache.Get("same", true, FakeSources, CancellationToken.None));
Check(fetches == 1, "simultaneous recent-movie source requests share one upstream lookup");
cacheNow = cacheNow.AddMinutes(1);
await sourceCache.Get("same", true, FakeSources, CancellationToken.None);
Check(fetches == 2, "short candidate cache expires after one minute");
await sourceCache.Get("same", false, FakeSources, CancellationToken.None);
Check(fetches == 3, "ordinary movies keep fresh on-demand source behavior");
var emptyFetches = 0;
Task<JsonElement> FakeEmpty(CancellationToken _) { emptyFetches++; return Task.FromResult(empty.RootElement); }
await sourceCache.Get("empty", true, FakeEmpty, CancellationToken.None);
await sourceCache.Get("empty", true, FakeEmpty, CancellationToken.None);
Check(emptyFetches == 1, "completed empty responses also suppress immediate duplicate indexer searches");
var failedFetches = 0;
Task<JsonElement> FakeFailure(CancellationToken _) { failedFetches++; throw new HttpRequestException("fixture failure"); }
for (var attempt = 0; attempt < 2; attempt++)
    try { await sourceCache.Get("error", true, FakeFailure, CancellationToken.None); }
    catch (HttpRequestException) { }
Check(failedFetches == 2, "request failures are never cached as empty candidate responses");
using var malformed = JsonDocument.Parse("{\"MediaSources\":null}");
try { await sourceCache.Get("malformed", true, _ => Task.FromResult(malformed.RootElement), CancellationToken.None); throw new Exception("Accepted malformed sources"); }
catch (JsonException) { checks++; Console.WriteLine("PASS malformed source response is an error rather than an empty search"); }
Console.WriteLine($"{checks} contract checks passed.");
