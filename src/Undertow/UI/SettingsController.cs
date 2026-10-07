using System.ComponentModel;
using System.Text.Json;
using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Common;
using Emby.Web.GenericEdit.Elements;
using Emby.Web.GenericEdit.Elements.List;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Plugins.UI;
using MediaBrowser.Model.Plugins.UI.Views;
using MediaBrowser.Model.Tasks;
namespace Jellyembifier.UI;

public sealed class SettingsController(string id) : ControllerBase(id), IHasTabbedUIPages
{
    public override PluginPageInfo PageInfo => new() { Name = "JellyembifierSettings", DisplayName = "Connection & Catalogs", IsMainConfigPage = true, MenuSection = "server", MenuIcon = "settings" };
    private readonly IReadOnlyList<IPluginUIPageController> tabs = new IPluginUIPageController[]
    {
        new TabController(id, "2Maintenance", "Maintenance", true)
    };
    public IReadOnlyList<IPluginUIPageController> TabPageControllers => tabs;
    public override async Task<IPluginUIView> CreateDefaultPageView() { var view = new ConnectionView(PluginId); await view.LoadCatalogs(); return view; }
}
public sealed class TabController(string id, string name, string display, bool maintenance) : ControllerBase(id)
{
    public override PluginPageInfo PageInfo => new() { Name = name, DisplayName = display };
    public override async Task<IPluginUIView> CreateDefaultPageView()
    {
        if (maintenance) return new MaintenanceView(PluginId);
        var view = new ConnectionView(PluginId); await view.LoadCatalogs(); return view;
    }
}
public sealed class ConnectionOptions : EditableOptionsBase
{
    public override string EditorTitle => "Connection & Catalogs";
    public override string EditorDescription => "Connect your media source, choose catalogs and name your channel folders. Save your connection, then use Maintenance to refresh the catalog.";
    [DisplayName("Server URL")]
    [Description("Enter your Jellyfin server address, including any URL base path. Compatible sources may supply a profile path.")]
    public string ServerUrl { get; set; } = "";
    [DisplayName("Username / profile ID")]
    [Description("Enter your Jellyfin account username, or the username/profile ID supplied by a compatible source.")]
    public string ProfileId { get; set; } = "";
    [DisplayName("Password")]
    [Description("Leave blank to retain the saved password.")]
    [MediaBrowser.Model.Attributes.IsPassword]
    public string Password { get; set; } = "";
    public CaptionItem Names { get; set; } = new("Channel folder names");
    [DisplayName("Movies")]
    public string MoviesName { get; set; } = "Movies";
    [DisplayName("Series")]
    public string SeriesName { get; set; } = "Series";
    [DisplayName("Anime")]
    public string AnimeName { get; set; } = "Anime";
    [DisplayName("Anime Movies")]
    public string AnimeMoviesName { get; set; } = "Movies";
    [DisplayName("Anime Series")]
    public string AnimeSeriesName { get; set; } = "Series";
    public CaptionItem CatalogCaption { get; set; } = new("Upstream catalogs");
    public LabelItem CatalogHelp { get; set; } = new("Catalog changes save immediately. Turn off a catalog to stop new additions and keep existing titles. Mark anime catalogs to place their titles under Anime.");
    public GenericItemList Catalogs { get; set; } = new();
    public StatusItem Result { get; set; } = new("Connection", "Not checked", ItemStatus.None);
    public ButtonItem Test { get; set; } = new("Test saved connection / reload catalogs") { Data1 = "Test", Icon = IconNames.refresh };
}
public sealed class ConnectionView : PluginPageView
{
    private readonly List<UpstreamItem> available = new();
    public ConnectionView(string id) : base(id)
    {
        var c = Plugin.Instance.Configuration; var connection = ConnectionStore.Load();
        ContentData = new ConnectionOptions { ServerUrl = connection.Url, ProfileId = connection.Username,
            MoviesName = c.MoviesName, SeriesName = c.SeriesName, AnimeName = c.AnimeName, AnimeMoviesName = c.AnimeMoviesName, AnimeSeriesName = c.AnimeSeriesName };
    }
    public async Task LoadCatalogs()
    {
        var ui = (ConnectionOptions)ContentData;
        try
        {
            available.Clear(); available.AddRange(await JellyfinClient.Instance.Views(CancellationToken.None)); ui.Catalogs.Clear();
            var c = Plugin.Instance.Configuration;
            foreach (var row in available)
            {
                var enabled = c.CatalogIds.Length == 0 || c.CatalogIds.Contains(row.Id);
                var anime = c.AnimeCatalogIds.Contains(row.Id);
                ui.Catalogs.Add(new GenericListItem { PrimaryText = row.Name, SecondaryText = (enabled ? "Included" : "Disabled") + (anime ? " · Anime catalog" : ""),
                    Icon = IconNames.folder, IconMode = ItemListIconMode.SmallRegular,
                    Toggle = new ToggleButtonItem { IsChecked = enabled, Caption = "Include", Data1 = row.Id, Data2 = row.Id, CommandId = "ToggleCatalog" },
                    Button1 = new ButtonItem(anime ? "Unmark Anime" : "Mark Anime") { Data1 = row.Id, Data2 = row.Id, CommandId = "ToggleAnime", Icon = IconNames.edit } });
            }
            ui.Result.StatusText = available.Count + " catalogs available"; ui.Result.Status = ItemStatus.Succeeded;
        }
        catch { ui.Result.StatusText = "Could not load catalogs. Check the saved server URL, username / profile ID and password."; ui.Result.Status = ItemStatus.Failed; }
    }
    private void RequireAdmin() { if (User?.Policy?.IsAdministrator != true) throw new UnauthorizedAccessException("Administrator required."); }
    public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
    {
        if (commandId == "PageSave") return await OnSaveCommand(itemId, commandId, data);
        RequireAdmin(); var command = string.IsNullOrEmpty(commandId) ? data.Split(':')[0] : commandId;
        if (command is "ToggleCatalog" or "ToggleAnime")
        {
            if (!available.Any(x => x.Id == itemId)) throw new ArgumentException("Unknown catalog.");
            var c = Plugin.Instance.Configuration;
            var selected = command == "ToggleAnime" ? c.AnimeCatalogIds.ToHashSet() : (c.CatalogIds.Length == 0 ? available.Select(x => x.Id).ToHashSet() : c.CatalogIds.ToHashSet());
            if (!selected.Remove(itemId)) selected.Add(itemId);
            if (command == "ToggleCatalog" && selected.Count == 0) throw new InvalidOperationException("Keep at least one catalog selected.");
            if (command == "ToggleAnime") c.AnimeCatalogIds = selected.ToArray(); else c.CatalogIds = selected.ToArray();
            Plugin.Instance.SaveConfiguration();
        }
        await LoadCatalogs(); return this;
    }
    public override async Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
    {
        RequireAdmin(); var ui = (ConnectionOptions)ContentData;
        using var values = JsonDocument.Parse(data);
        var value = values.RootElement;
        ui.ServerUrl = value.GetProperty(nameof(ui.ServerUrl)).GetString() ?? "";
        ui.ProfileId = value.GetProperty(nameof(ui.ProfileId)).GetString() ?? "";
        ui.Password = value.GetProperty(nameof(ui.Password)).GetString() ?? "";
        ui.MoviesName = value.GetProperty(nameof(ui.MoviesName)).GetString() ?? "";
        ui.SeriesName = value.GetProperty(nameof(ui.SeriesName)).GetString() ?? "";
        ui.AnimeName = value.GetProperty(nameof(ui.AnimeName)).GetString() ?? "";
        ui.AnimeMoviesName = value.GetProperty(nameof(ui.AnimeMoviesName)).GetString() ?? "";
        ui.AnimeSeriesName = value.GetProperty(nameof(ui.AnimeSeriesName)).GetString() ?? "";
        var names = new[] { ui.MoviesName, ui.SeriesName, ui.AnimeName, ui.AnimeMoviesName, ui.AnimeSeriesName };
        if (names.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100)) throw new ArgumentException("Folder names must contain 1–100 characters.");
        ConnectionStore.Save(ui.ServerUrl.Trim(), ui.ProfileId.Trim(), ui.Password);
        var c = Plugin.Instance.Configuration; c.MoviesName = ui.MoviesName.Trim(); c.SeriesName = ui.SeriesName.Trim(); c.AnimeName = ui.AnimeName.Trim(); c.AnimeMoviesName = ui.AnimeMoviesName.Trim(); c.AnimeSeriesName = ui.AnimeSeriesName.Trim();
        Plugin.Instance.SaveConfiguration(); ui.Password = ""; await FolderArtwork.Apply(Plugin.Instance.Host, CancellationToken.None); return this;
    }
}
public sealed class MaintenanceOptions : EditableOptionsBase
{
    public override string EditorTitle => "Maintenance";
    public override string EditorDescription => "Refresh catalogs, check sync status and choose playback preferences. Each sync keeps existing titles and watch progress.";
    public StatusItem State { get; set; } = new("Library status", "Ready", ItemStatus.None);
    public CaptionItem Summary { get; set; } = new("Your catalog");
    public LabelItem Counts { get; set; } = new("");
    public LabelItem LastSync { get; set; } = new("");
    public LabelItem Duration { get; set; } = new("");
    public LabelItem NextSync { get; set; } = new("");
    public ButtonItem Reload { get; set; } = new("Refresh status") { Data1 = "Status", Icon = IconNames.refresh };
    public CaptionItem Schedule { get; set; } = new("Automatic refresh");
    [DisplayName("Enable scheduled refresh")]
    public bool Enabled { get; set; }
    [DisplayName("Refresh interval (hours)")]
    [Description("1–168 hours. Emby checks hourly. Save changes to apply your schedule.")]
    public int RefreshHours { get; set; } = 6;
    [DisplayName("Refresh existing seasons and episodes every (hours)")]
    [Description("1–720 hours. New series are fetched on the next sync. Existing series reuse cached episode metadata for this period.")]
    public int SeriesRefreshHours { get; set; } = 6;
    public CaptionItem Actions { get; set; } = new("Refresh your library");
    public LabelItem RefreshHelp { get; set; } = new("Check selected catalogs for additions and update Emby. Existing titles and watch progress are kept. Files are opened and probed when playback starts.");
    public ButtonItem Refresh { get; set; } = new("Refresh now") { Data1 = "Refresh", Icon = IconNames.refresh };
    public ButtonItem CancelRun { get; set; } = new("Cancel current refresh") { Data1 = "Cancel", Icon = IconNames.close };
    public CaptionItem Recovery { get; set; } = new("Full rebuild");
    public LabelItem RebuildHelp { get; set; } = new("Fetch every selected catalog, season and episode again, then update Emby. Use this to recheck cached metadata. Existing titles and watch progress are kept.");
    public ButtonItem Rebuild { get; set; } = new("Rebuild catalog…") { Data1 = "Rebuild", Icon = IconNames.warning, ConfirmationPrompt = "Rebuild the catalog? All selected catalogs, seasons and episodes will be fetched again. Titles and watch progress are retained. This can take several minutes." };
    public CaptionItem Unavailable { get; set; } = new("Recent unavailable titles");
    public LabelItem UnavailableHelp { get; set; } = new("Reasons from recent playback requests. No background searches. The last ten misses are kept until Emby restarts; a successful source lookup clears that title.");
    public LabelItem UnavailableTitles { get; set; } = new("");
    public CaptionItem Playback { get; set; } = new("Playback selection");
    [DisplayName("Maximum playback versions")]
    [Description("1–50 versions per title. Your source may return fewer. Configure release filters in your source.")]
    public int MaximumVersions { get; set; } = 12;
    [DisplayName("Preferred 4K size (GB)")]
    [Description("1–200 GB. Rank 4K versions nearest this size first. This is a preference, not a size limit; Emby may reorder versions for your player.")]
    public int Preferred4KSizeGb { get; set; } = 20;
}
public sealed class MaintenanceView : PluginPageView
{
    public MaintenanceView(string id) : base(id)
    {
        var c = Plugin.Instance.Configuration;
        ContentData = new MaintenanceOptions { Enabled = c.Enabled, RefreshHours = c.RefreshHours, SeriesRefreshHours = c.EffectiveSeriesRefreshHours, MaximumVersions = c.MaximumVersions, Preferred4KSizeGb = c.Preferred4KSizeGb };
        UpdateStatus();
    }
    private void UpdateStatus()
    {
        var ui = (MaintenanceOptions)ContentData; var s = CatalogSync.Snapshot;
        var running = CatalogSync.Phase is "Catalog listing" or "Season and episode metadata" or "Emby import and native metadata";
        ui.State.Status = running ? ItemStatus.InProgress : string.IsNullOrEmpty(CatalogSync.LastError) ? (s.Imported == default ? ItemStatus.None : ItemStatus.Succeeded) : ItemStatus.Failed;
        ui.State.StatusText = running ? CatalogSync.Phase + $" · {CatalogSync.CatalogPages:N0} pages · {CatalogSync.SeriesProcessed:N0} series checked" : string.IsNullOrEmpty(CatalogSync.LastError) ? (s.Imported == default ? "Waiting for first sync" : "Up to date with the last completed sync") : CatalogSync.LastError;
        ui.Refresh.IsEnabled = !running;
        ui.Rebuild.IsEnabled = !running;
        ui.CancelRun.IsVisible = running;
        var children = s.Children.Values.SelectMany(x => x).DistinctBy(x => x.Id).ToList();
        ui.Counts.Text = $"{s.Items.Count(x => x.Type == "Movie"):N0} movies  ·  {s.Items.Count(x => x.Type == "Series"):N0} series  ·  {children.Count(x => x.Type == "Season"):N0} seasons  ·  {children.Count(x => x.Type == "Episode"):N0} episodes\nCatalog snapshot counts, including retained titles and anime. Emby may combine duplicate records.";
        ui.LastSync.Text = "Last successful sync: " + (s.Imported == default ? "Not yet completed" : s.Imported.ToLocalTime().ToString("MMM d, yyyy · h:mm tt zzz"));
        var total = TimeSpan.FromSeconds(s.CatalogSeconds + s.StructureSeconds + s.ImportSeconds);
        ui.Duration.Text = s.Imported == default ? "Duration: available after the first sync" : $"Last sync took {(int)total.TotalMinutes}m {total.Seconds}s · Catalogs {s.CatalogSeconds:F1}s · Seasons / episodes {s.StructureSeconds:F1}s · Emby {s.ImportSeconds:F1}s";
        var misses = PlaybackDiagnostics.Snapshot();
        ui.UnavailableTitles.Text = misses.Count == 0 ? "No unavailable titles recorded since restart." : string.Join("\n\n", misses.Select(x => $"{x.Title} · {x.CheckedAt.ToLocalTime():MMM d, h:mm tt}\n{x.Reason}"));
        var c = Plugin.Instance.Configuration;
        ui.NextSync.Text = !c.Enabled ? "Automatic refresh is disabled" : s.Imported == default ? "Next sync: first scheduled check" : "Next sync due: " + s.Imported.AddHours(c.RefreshHours).ToLocalTime().ToString("MMM d · h:mm tt zzz") + " (at the next hourly check)";

    }
    private void RequireAdmin() { if (User?.Policy?.IsAdministrator != true) throw new UnauthorizedAccessException("Administrator required."); }
    public override async Task<IPluginUIView> RunCommand(string itemId, string commandId, string data)
    {
        if (commandId == "PageSave") return await OnSaveCommand(itemId, commandId, data);
        RequireAdmin(); var command = string.IsNullOrEmpty(commandId) ? data.Split(':')[0] : commandId;
        if (command is "Refresh" or "Rebuild")
        {
            if (CatalogSync.Phase is "Catalog listing" or "Season and episode metadata" or "Emby import and native metadata") throw new InvalidOperationException("A refresh is already running.");
            CatalogRefreshTask.ForceNextRun = true; CatalogRefreshTask.RebuildNextRun = command == "Rebuild";
            Plugin.Instance.Host.Resolve<ITaskManager>().QueueIfNotRunning<CatalogRefreshTask>();
        }
        else if (command == "Cancel") Plugin.Instance.Host.Resolve<ITaskManager>().CancelIfRunning<CatalogRefreshTask>();
        UpdateStatus(); return this;
    }
    public override Task<IPluginUIView> OnSaveCommand(string itemId, string commandId, string data)
    {
        RequireAdmin(); var ui = (MaintenanceOptions)ContentData;
        using var values = JsonDocument.Parse(data);
        var value = values.RootElement;
        ui.Enabled = value.GetProperty(nameof(ui.Enabled)).GetBoolean();
        ui.RefreshHours = SettingsValues.Integer(value, nameof(ui.RefreshHours));
        ui.SeriesRefreshHours = SettingsValues.Integer(value, nameof(ui.SeriesRefreshHours));
        ui.MaximumVersions = SettingsValues.Integer(value, nameof(ui.MaximumVersions));
        ui.Preferred4KSizeGb = SettingsValues.Integer(value, nameof(ui.Preferred4KSizeGb));
        if (ui.RefreshHours is < 1 or > 168 || ui.SeriesRefreshHours is < 1 or > 720 || ui.MaximumVersions is < 1 or > 50 || ui.Preferred4KSizeGb is < 1 or > 200) throw new ArgumentException("Setting outside its supported range.");
        var c = Plugin.Instance.Configuration; c.Enabled = ui.Enabled; c.RefreshHours = ui.RefreshHours; c.SeriesRefreshHours = ui.SeriesRefreshHours; c.MaximumVersions = ui.MaximumVersions; c.Preferred4KSizeGb = ui.Preferred4KSizeGb;
        Plugin.Instance.SaveConfiguration(); UpdateStatus(); return Task.FromResult<IPluginUIView>(this);
    }
}

public static class SettingsValues
{
    // Emby's native number inputs submit strings; API callers may submit JSON numbers.
    public static int Integer(JsonElement values, string name)
    {
        var value = values.GetProperty(name);
        return value.ValueKind == JsonValueKind.Number ? value.GetInt32() :
            int.Parse(value.GetString() ?? "", System.Globalization.CultureInfo.InvariantCulture);
    }
}
