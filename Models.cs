using System.Text.Json.Serialization;

namespace WowLauncher;

/// <summary>The remote manifest, hosted on GitHub (raw manifest.json).</summary>
public class Manifest
{
    [JsonPropertyName("serverName")]  public string ServerName  { get; set; } = "WoW Server";
    [JsonPropertyName("realmlist")]   public string Realmlist   { get; set; } = "127.0.0.1";
    [JsonPropertyName("statusHost")]  public string StatusHost  { get; set; } = "127.0.0.1";
    [JsonPropertyName("statusPort")]  public int    StatusPort  { get; set; } = 3724;
    [JsonPropertyName("registerUrl")] public string RegisterUrl { get; set; } = "";
    [JsonPropertyName("websiteUrl")]  public string WebsiteUrl  { get; set; } = "";
    [JsonPropertyName("news")]        public List<NewsItem> News    { get; set; } = new();
    [JsonPropertyName("patches")]     public List<PatchItem> Patches { get; set; } = new();
}

public class NewsItem
{
    [JsonPropertyName("date")]  public string Date  { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("body")]  public string Body  { get; set; } = "";
}

public class PatchItem
{
    /// <summary>Filename as it must appear in the client, e.g. "patch-4.MPQ" or "patch-enUS-4.MPQ".</summary>
    [JsonPropertyName("file")] public string File { get; set; } = "";
    /// <summary>Subfolder to place the file in. When root=false this is under Data\ ("" = Data\, "enUS" = Data\enUS\). When root=true it is under the game folder itself ("" = game root, next to Wow.exe).</summary>
    [JsonPropertyName("dest")] public string Dest { get; set; } = "";
    /// <summary>When true, place the file relative to the game folder (root) instead of under Data\. Used for Wow.exe and helper DLLs.</summary>
    [JsonPropertyName("root")] public bool Root { get; set; } = false;
    /// <summary>Direct download URL (e.g. a GitHub release asset).</summary>
    [JsonPropertyName("url")]  public string Url  { get; set; } = "";
    /// <summary>MD5 of the file (lowercase hex). Used to detect changes.</summary>
    [JsonPropertyName("md5")]  public string Md5  { get; set; } = "";
    /// <summary>Size in bytes (used for the progress bar; optional).</summary>
    [JsonPropertyName("size")] public long   Size { get; set; }
}

/// <summary>Local launcher.json next to the exe. Lets you change settings without recompiling.</summary>
public class LocalConfig
{
    /// <summary>Where to fetch the manifest from (GitHub raw URL).</summary>
    [JsonPropertyName("manifestUrl")] public string ManifestUrl { get; set; } = "";
    /// <summary>Optional override for the game folder. Defaults to the launcher's own folder.</summary>
    [JsonPropertyName("gamePath")]    public string GamePath    { get; set; } = "";
}
