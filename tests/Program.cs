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
Console.WriteLine($"{checks} contract checks passed.");
