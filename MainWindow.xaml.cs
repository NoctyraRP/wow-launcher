using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace WowLauncher;

public partial class MainWindow : Window
{
    // Fallback if there is no launcher.json next to the exe. Edit to your GitHub raw URL,
    // or (better) ship a launcher.json so you never have to recompile.
    private const string DefaultManifestUrl =
        "https://raw.githubusercontent.com/USER/REPO/main/manifest.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private string _gamePath = "";
    private string _dataPath = "";
    private Manifest _manifest = new();

    public MainWindow()
    {
        InitializeComponent();
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("WowLauncher/1.0");
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var cfg = LoadLocalConfig();
        _gamePath = string.IsNullOrWhiteSpace(cfg.GamePath) ? AppContext.BaseDirectory : cfg.GamePath;
        _dataPath = Path.Combine(_gamePath, "Data");
        var manifestUrl = string.IsNullOrWhiteSpace(cfg.ManifestUrl) ? DefaultManifestUrl : cfg.ManifestUrl;

        // 1. Fetch the manifest
        SetProgress("Checking for updates...", null);
        try
        {
            var json = await FetchTextAsync(manifestUrl);
            _manifest = JsonSerializer.Deserialize<Manifest>(json) ?? new Manifest();
        }
        catch
        {
            SetProgress("Couldn't reach the update server — you can still play with what you have.", 0);
            _manifest = new Manifest();
        }
        ApplyManifestToUi(_manifest);

        // 2. Server status (non-blocking)
        _ = CheckServerStatusAsync();

        // 3. Update patches
        try
        {
            await UpdatePatchesAsync(_manifest);
            SetProgress("Up to date. Ready to play!", 100);
        }
        catch (Exception ex)
        {
            SetProgress("Update error: " + ex.Message, 0);
        }

        PlayButton.IsEnabled = true;
    }

    // Fetch manifest text from either an http(s) URL or a local/UNC file path (for LAN or testing).
    private static async Task<string> FetchTextAsync(string source)
    {
        bool isLocal = source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                       || source.StartsWith(@"\\")
                       || (source.Length > 1 && source[1] == ':');
        if (isLocal)
        {
            var path = source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? new Uri(source).LocalPath
                : source;
            return await File.ReadAllTextAsync(path);
        }
        return await Http.GetStringAsync(source);
    }

    // ---------- Config ----------
    private LocalConfig LoadLocalConfig()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "launcher.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<LocalConfig>(File.ReadAllText(path)) ?? new LocalConfig();
        }
        catch { /* ignore, use defaults */ }
        return new LocalConfig();
    }

    private void ApplyManifestToUi(Manifest m)
    {
        Title = m.ServerName + " Launcher";
        ServerNameText.Text = m.ServerName;
        RealmlistText.Text = "set realmlist " + m.Realmlist;
        NewsList.ItemsSource = m.News;
        RegisterButton.Visibility = string.IsNullOrWhiteSpace(m.RegisterUrl) ? Visibility.Collapsed : Visibility.Visible;
        WebsiteButton.Visibility  = string.IsNullOrWhiteSpace(m.WebsiteUrl)  ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- Server status ----------
    private async Task CheckServerStatusAsync()
    {
        bool online = false;
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(_manifest.StatusHost, _manifest.StatusPort);
            var done = await Task.WhenAny(connect, Task.Delay(4000));
            online = done == connect && client.Connected;
        }
        catch { online = false; }

        StatusDot.Fill = (Brush)FindResource(online ? "Online" : "Offline");
        StatusText.Text = online ? "Server Online" : "Server Offline";
        StatusText.Foreground = (Brush)FindResource(online ? "Online" : "Offline");
    }

    // ---------- Patch updating ----------
    private async Task UpdatePatchesAsync(Manifest m)
    {
        if (m.Patches.Count == 0)
        {
            SetProgress("No patches configured — ready to play.", 100);
            return;
        }

        Directory.CreateDirectory(_dataPath);

        // Figure out which patches actually need downloading
        var toDownload = new List<PatchItem>();
        int idx = 0;
        foreach (var p in m.Patches)
        {
            idx++;
            SetProgress($"Verifying {p.File} ({idx}/{m.Patches.Count})...", null);
            var baseDir = p.Root ? _gamePath : _dataPath;
            var local = Path.Combine(baseDir, p.Dest, p.File);
            if (!File.Exists(local))
            {
                toDownload.Add(p);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(p.Md5))
            {
                var localMd5 = await Task.Run(() => FileMd5(local));
                if (!localMd5.Equals(p.Md5, StringComparison.OrdinalIgnoreCase))
                    toDownload.Add(p);
            }
        }

        if (toDownload.Count == 0)
            return;

        int n = 0;
        foreach (var p in toDownload)
        {
            n++;
            await DownloadPatchAsync(p, n, toDownload.Count);
        }
    }

    private async Task DownloadPatchAsync(PatchItem p, int index, int total)
    {
        var baseDir = p.Root ? _gamePath : _dataPath;
        var targetDir = Path.Combine(baseDir, p.Dest);
        Directory.CreateDirectory(targetDir);
        var dest = Path.Combine(targetDir, p.File);
        var tmp = dest + ".part";

        using var resp = await Http.GetAsync(p.Url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        long totalBytes = resp.Content.Headers.ContentLength ?? p.Size;

        await using (var input = await resp.Content.ReadAsStreamAsync())
        await using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
        {
            var buffer = new byte[1 << 20]; // 1 MB
            long read = 0;
            int r;
            while ((r = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, r));
                read += r;
                double pct = totalBytes > 0 ? read * 100.0 / totalBytes : 0;
                SetProgress($"Downloading {p.File}  ({index}/{total})  —  {Mb(read)} / {Mb(totalBytes)}", pct);
            }
        }

        if (File.Exists(dest)) File.Delete(dest);
        File.Move(tmp, dest);
    }

    private static string Mb(long bytes) => bytes <= 0 ? "?" : $"{bytes / 1024.0 / 1024.0:0.0} MB";

    private static string FileMd5(string path)
    {
        using var md5 = MD5.Create();
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(md5.ComputeHash(fs)).ToLowerInvariant();
    }

    // ---------- Play ----------
    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        // 1. Write realmlist into every locale folder we find (and enUS as fallback)
        try
        {
            var content = "set realmlist " + _manifest.Realmlist + "\n";
            bool wroteAny = false;
            if (Directory.Exists(_dataPath))
            {
                foreach (var dir in Directory.GetDirectories(_dataPath))
                {
                    var name = Path.GetFileName(dir);
                    if (name.Length == 4 && char.IsLetter(name[0])) // enUS, enGB, deDE, ...
                    {
                        File.WriteAllText(Path.Combine(dir, "realmlist.wtf"), content);
                        wroteAny = true;
                    }
                }
            }
            if (!wroteAny)
            {
                var enus = Path.Combine(_dataPath, "enUS");
                Directory.CreateDirectory(enus);
                File.WriteAllText(Path.Combine(enus, "realmlist.wtf"), content);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not write realmlist: " + ex.Message, "Launcher",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // 2. Launch the game
        var wow = Path.Combine(_gamePath, "Wow.exe");
        if (!File.Exists(wow))
        {
            MessageBox.Show(
                $"Wow.exe was not found in:\n{_gamePath}\n\n" +
                "Put the launcher in your WoW folder, or set \"gamePath\" in launcher.json.",
                "Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = wow, WorkingDirectory = _gamePath, UseShellExecute = true });
            Application.Current.Shutdown(); // close the launcher once the game starts
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not start Wow.exe: " + ex.Message, "Launcher",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e) => OpenUrl(_manifest.RegisterUrl);
    private void WebsiteButton_Click(object sender, RoutedEventArgs e) => OpenUrl(_manifest.WebsiteUrl);

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    // ---------- UI helpers ----------
    private void SetProgress(string text, double? pct)
    {
        ProgressText.Text = text;
        if (pct.HasValue)
        {
            Progress.IsIndeterminate = false;
            Progress.Value = Math.Clamp(pct.Value, 0, 100);
        }
        else
        {
            Progress.IsIndeterminate = true;
        }
    }
}
