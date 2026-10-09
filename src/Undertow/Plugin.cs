using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Common;

namespace Jellyembifier;

public sealed class Configuration : BasePluginConfiguration
{
    public string MoviesName { get; set; } = "Movies";
    public string SeriesName { get; set; } = "Series";
    public string AnimeName { get; set; } = "Anime";
    public string AnimeMoviesName { get; set; } = "Movies";
    public string AnimeSeriesName { get; set; } = "Series";
    public bool Enabled { get; set; }
    public bool SkipLibraryDuplicates { get; set; }
    public bool CheckRecentMovieAvailability { get; set; }
    public string[] CatalogIds { get; set; } = Array.Empty<string>();
    public string[] AnimeCatalogIds { get; set; } = Array.Empty<string>();
    public int MaximumVersions { get; set; } = 12;
    public int Preferred4KSizeGb { get; set; } = 20;
    public int RefreshHours { get; set; } = 6;
    private int seriesRefreshHours;
    public int SeriesRefreshHours
    {
        get => seriesRefreshHours > 0 ? seriesRefreshHours : SeriesRefreshDays > 0 ? Math.Clamp(SeriesRefreshDays, 1, 30) * 24 : 6;
        set => seriesRefreshHours = value;
    }
    // Read older configurations without changing their cache interval.
    public int SeriesRefreshDays { get; set; }
    [System.Xml.Serialization.XmlIgnore, System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveSeriesRefreshHours => Math.Clamp(SeriesRefreshHours, 1, 720);
    public bool ShouldSerializeSeriesRefreshDays() => false;
}

public sealed class Plugin : BasePlugin<Configuration>, IHasUIPages, IHasThumbImage
{
    public string DataDirectory { get; }
    public IApplicationHost Host { get; }
    private IReadOnlyCollection<IPluginUIPageController>? pages;
    public IReadOnlyCollection<IPluginUIPageController> UIPageControllers => pages ??= new IPluginUIPageController[] { new UI.SettingsController(Id.ToString()) };
    public static Plugin Instance { get; private set; } = null!;
    public static readonly Guid PluginId = new("cbadf6bd-8d7a-4e58-bf29-9b0cc91d78c7");
    public override Guid Id => PluginId;
    public override string Name => "Undertow";
    public override string Description => "A Jellyfin-to-Emby library bridge, with scheduled metadata sync and playback on demand.";
    // Keep the existing configuration file and plugin GUID across the rebrand.
    public override string ConfigurationFileName => "Jellyembifier.xml";
    public MediaBrowser.Model.Drawing.ImageFormat ThumbImageFormat => MediaBrowser.Model.Drawing.ImageFormat.Png;
    public Stream GetThumbImage() => GetType().Assembly.GetManifestResourceStream("Jellyembifier.channel.png")!;
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer, IApplicationHost host) : base(paths, serializer)
    {
        Instance = this;
        Host = host;
        DataDirectory = Path.Combine(paths.DataPath, "jellyembifier");
    }
}
