using System.Text.Json;
namespace Jellyembifier;
public static class ConnectionStore
{
    private static string FileName => Path.Combine(Plugin.Instance.DataDirectory, "connection.json");
    public static UpstreamSettings Load()
    {
        var file = FileName;
        if (!File.Exists(file)) return new();
        return JsonSerializer.Deserialize<UpstreamSettings>(File.ReadAllText(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Connection not configured.");
    }
    public static void Save(string url, string profileId, string password)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo != "" || uri.Query != "" || uri.Fragment != "")
            throw new ArgumentException("Enter an HTTP or HTTPS API address. Put your username and password in their own fields; omit query parameters and fragments.");
        var current = Load();
        if (string.IsNullOrWhiteSpace(profileId)) throw new ArgumentException("Username/profile ID is required.");
        if (CatalogSync.Snapshot.Completed != default && (url.TrimEnd('/') != current.Url.TrimEnd('/') || profileId != current.Username))
            throw new InvalidOperationException("This catalog belongs to the saved source. Use a separate Emby instance for a different server or profile. You can update the password here.");
        var changed = current.Url.TrimEnd('/') != url.TrimEnd('/') || current.Username != profileId || (!string.IsNullOrEmpty(password) && current.Password != password);
        if (!changed) return;
        if (CatalogSync.IsRunning) throw new InvalidOperationException("Wait for the current sync before changing the connection.");
        current.Url = url.TrimEnd('/'); current.Username = profileId;
        if (!string.IsNullOrEmpty(password)) current.Password = password;
        if (string.IsNullOrEmpty(current.Password)) throw new ArgumentException("Password is required.");
        Directory.CreateDirectory(Plugin.Instance.DataDirectory);
        var temporary = FileName + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            JsonSerializer.Serialize(stream, current);
        }
        File.Move(temporary, FileName, true);
        JellyfinClient.Reset();
    }
}
