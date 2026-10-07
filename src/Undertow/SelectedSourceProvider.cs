using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Text.RegularExpressions;
using MediaBrowser.Common;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyembifier;

// Emby's built-in channel provider cannot open sources. This provider owns only
// this channel's sources, so Emby's selected-source opening can invoke its probe.
public sealed class SelectedSourceProvider(IApplicationHost host) : IMediaSourceProvider
{
    public static int OpenCalls;
    public static int ProbeCalls;
    public static int ProbeFailures;
    public static int CloseCalls;
    public static int LastAudioTrackCount;

    public async Task<List<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken ct)
    {
        if (item is not Video) return new();
        var channel = host.Resolve<IChannelManager>().GetChannel<JellyfinChannel>();
        if (channel == null || item.FindParent<Channel>()?.Id !=
            host.Resolve<ILibraryManager>().GetNewItemId("Channel " + channel.Name, typeof(Channel))) return new();
        var sources = (await new JellyfinChannel().GetChannelItemMediaInfo(item.ExternalId, ct)).ToList();
        foreach (var source in sources)
        {
            PrepareUnopenedSource(source);
            source.RequiresOpening = true;
            source.OpenToken = MakeToken(item.ExternalId, source.Id);
        }
        return sources;
    }

    public static void PrepareUnopenedSource(MediaSourceInfo source)
    {
        // Upstream audio/subtitle hints are not real track identities. Advertising
        // them makes clients pin a guessed index before the selected-source probe.
        // Retain numbered video hints so native enumeration does not probe every file.
        source.MediaStreams = source.MediaStreams.Where(t => t.Type == MediaStreamType.Video).ToList();
        source.DefaultAudioStreamIndex = null;
        source.DefaultSubtitleStreamIndex = null;
    }

    public static void ExcludeDuplicateLanguageCommentary(MediaSourceInfo source)
    {
        bool Commentary(MediaStream t) => Regex.IsMatch(t.Title ?? "", @"\bcommentary\b", RegexOptions.IgnoreCase);
        var originals = source.MediaStreams.Where(t => t.Type == MediaStreamType.Audio &&
            !string.IsNullOrWhiteSpace(t.Language) && !Commentary(t)).Select(t => t.Language).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Emby 4.10 has no commentary flag and may prefer a browser-compatible
        // commentary codec over the original. Omit only labelled commentary with
        // another soundtrack in the same language; preserve the real indices.
        source.MediaStreams = source.MediaStreams.Where(t => t.Type != MediaStreamType.Audio ||
            !Commentary(t) || !originals.Contains(t.Language ?? "")).ToList();
    }

    public static string MakeToken(string itemId, string sourceId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(itemId + "\n" + sourceId));

    public static (string ItemId, string SourceId) ReadToken(string token)
    {
        if (token.Length > 4096) throw new ArgumentException("Invalid selected-source token.");
        var parts = Encoding.UTF8.GetString(Convert.FromBase64String(token)).Split('\n');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[1])) throw new ArgumentException("Invalid selected-source token.");
        JellyfinChannel.Decode(parts[0]);
        return (parts[0], parts[1]);
    }

    public async Task<ILiveStream> OpenMediaSource(string openToken, string consumerId,
        List<ILiveStream> currentLiveStreams, CancellationToken ct)
    {
        var selection = ReadToken(openToken);
        Interlocked.Increment(ref OpenCalls);
        // Revalidate the exact offered source; never silently switch versions.
        var sources = await new JellyfinChannel().GetChannelItemMediaInfo(selection.ItemId, ct);
        var source = sources.SingleOrDefault(x => x.Id == selection.SourceId)
            ?? throw new InvalidOperationException("Selected version is no longer offered.");
        source.MediaStreams = new();
        source.RequiresOpening = false;
        source.RequiresClosing = true;
        source.IsInfiniteStream = false;
        source.LiveStreamId = Guid.NewGuid().ToString("N");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        Interlocked.Increment(ref ProbeCalls);
        try
        {
            // Use Emby's own media-probe manager and stream normalization.
            await host.Resolve<IMediaSourceManager>().AddMediaInfoWithProbe(source,
                isAudio: false, addProbeDelay: false, timeout.Token);
            host.Resolve<IMediaSourceManager>().NormalizeMediaStreams(source.MediaStreams);
            if (!source.MediaStreams.Any(x => x.Type == MediaStreamType.Video && x.Index >= 0))
                throw new InvalidOperationException("Selected source probe returned no video track.");
            ExcludeDuplicateLanguageCommentary(source);
            Volatile.Write(ref LastAudioTrackCount, source.MediaStreams.Count(x => x.Type == MediaStreamType.Audio));
        }
        catch
        {
            Interlocked.Increment(ref ProbeFailures);
            throw;
        }
        return new SelectedStream(source, consumerId);
    }

    private sealed class SelectedStream : ILiveStream
    {
        private readonly ConcurrentDictionary<string, byte> consumers = new();
        public SelectedStream(MediaSourceInfo source, string consumerId)
        { MediaSource = source; AddConsumer(consumerId); }
        public int ConsumerCount => consumers.Count;
        public string OriginalStreamId { get; set; } = "";
        public string TunerHostId => "";
        public bool EnableStreamSharing => false;
        public MediaSourceInfo MediaSource { get; set; }
        public string UniqueId => MediaSource.LiveStreamId;
        public DateTimeOffset DateOpened { get; } = DateTimeOffset.UtcNow;
        public bool SupportsCopyTo => false;
        public Task Open(CancellationToken ct) => Task.CompletedTask;
        public Task Close() { consumers.Clear(); Interlocked.Increment(ref CloseCalls); return Task.CompletedTask; }
        public void AddConsumer(string id) => consumers.TryAdd(id ?? "", 0);
        public void RemoveConsumer(string id) => consumers.TryRemove(id ?? "", out _);
        public Task CopyToAsync(PipeWriter writer, CancellationToken ct) => throw new NotSupportedException();
        public Task CopyToAsync(Stream writer, DateTimeOffset? wallClockStartTime,
            Action<SegmentedStreamSegmentInfo> onSegmentWritten, CancellationToken ct) => throw new NotSupportedException();
    }
}
