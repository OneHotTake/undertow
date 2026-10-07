using MediaBrowser.Common;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;

namespace Jellyembifier;

// Embedded images are installed through Emby's native image API. No image server,
// private URLs or translated-name lookup are required.
public static class FolderArtwork
{
    public static string? AssetFor(string? externalId, UpstreamSettings settings) => externalId switch
    {
        "jf:anime" => "anime",
        "jf:anime:movies" => "anime-movies",
        "jf:anime:series" => "anime-series",
        _ when externalId == JellyfinChannel.Encode(settings.MovieLibraryId) => "movies",
        _ when externalId == JellyfinChannel.Encode(settings.SeriesLibraryId) => "series",
        _ => null
    };
    public static void ApplyChannelName(IApplicationHost host, CancellationToken ct)
    {
        var library = host.Resolve<ILibraryManager>();
        var channel = library.GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { "Channel" } }, ct)
            .OfType<Channel>().SingleOrDefault(x => x.Id == library.GetNewItemId("Channel " + JellyfinChannel.IdentityName, typeof(Channel)));
        if (channel == null || channel.Name == "Undertow") return;
        channel.Name = "Undertow"; channel.SortName = "Undertow";
        library.UpdateItem(channel, channel.GetParent(), ItemUpdateType.MetadataEdit);
    }
    public static async Task Apply(IApplicationHost host, CancellationToken ct)
    {
        var library = host.Resolve<ILibraryManager>();
        var channel = library.GetItemList(new InternalItemsQuery { IncludeItemTypes = new[] { "Channel" } }, ct)
            .OfType<Channel>().SingleOrDefault(x => x.Id == library.GetNewItemId("Channel " + JellyfinChannel.IdentityName, typeof(Channel)));
        if (channel == null) return;
        var source = JellyfinClient.Instance.Settings;
        var labels = JellyfinChannel.RootFolders(source, Plugin.Instance.Configuration).Concat(JellyfinChannel.AnimeFolders(Plugin.Instance.Configuration)).ToDictionary(x => JellyfinChannel.Encode(x.Id), x => x.Name);
        var roots = library.GetItemList(new InternalItemsQuery { ParentIds = new[] { channel.InternalId }, IncludeItemTypes = new[] { "Folder" } }, ct);
        var anime = roots.SingleOrDefault(x => x.ExternalId == "jf:anime");
        var folders = roots.Concat(anime == null ? Array.Empty<BaseItem>() : library.GetItemList(new InternalItemsQuery { ParentIds = new[] { anime.InternalId }, IncludeItemTypes = new[] { "Folder" } }, ct));
        var targets = folders.Where(x => labels.ContainsKey(x.ExternalId)).Select(x => (Item: x, Asset: AssetFor(x.ExternalId, source)!)).Prepend((Item: (BaseItem)channel, Asset: "channel"));
        var provider = host.Resolve<IProviderManager>();
        var directory = new DirectoryService(host.Resolve<IFileSystem>());
        foreach (var (item, asset) in targets)
        {
            ct.ThrowIfCancellationRequested();
            var name = asset == "channel" ? "Undertow" : labels.GetValueOrDefault(item.ExternalId ?? "");
            if (name != null && item.Name != name)
            {
                item.Name = name; item.SortName = name;
                library.UpdateItem(item, item.GetParent(), ItemUpdateType.MetadataEdit);
            }
            if (item.HasImage(ImageType.Primary, 0)) continue;
            var resource = asset == "channel" ? "Jellyembifier.channel-card.png" : $"Jellyembifier.Assets.{asset}.png";
            await using var stream = typeof(Plugin).Assembly.GetManifestResourceStream(resource) ?? throw new InvalidOperationException("Embedded artwork missing.");
            await provider.SaveImage(item, library.GetLibraryOptions(item), stream, "image/png".AsMemory(), ImageType.Primary, 0, Array.Empty<long>(), directory, true, ct);
            library.UpdateImages(item);
        }
    }
}
