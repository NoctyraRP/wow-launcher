# WoW Server Launcher

A custom launcher for your AzerothCore 3.3.5a server. It:

- **Auto-updates custom patches** — downloads changed `.MPQ` files into the client's `Data` folder.
- **Sets the realmlist and launches the game** with one Play button.
- **Shows news + live server status** (online/offline light).
- **Register / Website buttons** (shown when you set their URLs).

Everything players see is controlled by one file: **`manifest.json`**, which you host on GitHub.

---

## How it fits together

```
Launcher (on player's PC)  ──fetch──►  manifest.json        (small, on GitHub repo)
                           ──download►  patch-*.MPQ files    (large, GitHub Releases)
                           ──writes──►  <client>\Data\enUS\realmlist.wtf
                           ──starts──►  <client>\Wow.exe
```

- **`manifest.json`** — the small text file the launcher reads. Lives in your GitHub repo.
- **`launcher.json`** — local settings that ship *next to `WowLauncher.exe`*. Tells the launcher where the manifest is. Players never edit it.

---

## First-time setup (you, once)

### 1. Create a GitHub repo
e.g. `https://github.com/YOURNAME/wow-launcher`. Put `manifest.json` in it.

### 2. Point `launcher.json` at your manifest
Edit `launcher.json` and set the raw URL:
```json
{
  "manifestUrl": "https://raw.githubusercontent.com/YOURNAME/wow-launcher/main/manifest.json",
  "gamePath": ""
}
```
> Leave `gamePath` **empty** for players — the launcher then uses its own folder as the game
> folder (so you ship `WowLauncher.exe` + `launcher.json` inside the WoW client folder).
> Set `gamePath` only when testing from the build output.

### 3. Edit `manifest.json` for your server
Set `serverName`, `realmlist`, `statusHost`/`statusPort` (your public IP + `3724`),
`registerUrl`, `websiteUrl`, and the `news` list.

---

## Publishing a patch update (you, whenever you make a new patch)

1. Build your patch MPQ(s) with your patch tool, into one folder (e.g. `D:\Wow Private\Custom patches`).
2. **Upload each `.MPQ` as a GitHub *Release* asset** (Releases → create/edit a release, e.g. tag `patches`, drag the files in).
   *Use Releases, not the repo — repo files are capped at 100 MB; release assets allow up to 2 GB, which MPQs need.*
3. **Regenerate `manifest.json`** with the helper script (computes MD5 + size for every file):
   ```powershell
   .\tools\Make-Manifest.ps1 `
       -PatchFolder "D:\Wow Private\Custom patches" `
       -BaseUrl "https://github.com/YOURNAME/wow-launcher/releases/download/patches" `
       -Template ".\manifest.json" `
       -Output ".\manifest.json"
   ```
4. **Commit & push `manifest.json`** to the repo.

Next time anyone opens the launcher, it compares their local MPQs' MD5s to the manifest and
downloads only what changed. Done.

---

## Building the launcher

Dev build (fast, needs .NET runtime installed):
```powershell
dotnet build -c Release
```

**Distributable single .exe** (self-contained — players need nothing installed):
```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Output: `bin\Release\net9.0-windows\win-x64\publish\WowLauncher.exe`.
Ship that `WowLauncher.exe` + `launcher.json` inside the WoW client folder and give players the whole folder (or a zip).

---

## Files

| File | What it is |
|------|-----------|
| `WowLauncher.csproj`, `*.xaml`, `*.cs` | the launcher source |
| `launcher.json` | local settings (manifest URL, optional game path) — ships next to the exe |
| `manifest.json` | **the file you host on GitHub** — server info, news, patch list |
| `manifest.sample.json` | a documented example manifest |
| `tools\Make-Manifest.ps1` | scans a patch folder → generates `manifest.json` |

---

## Notes

- The manifest source can also be a **local or network path** (e.g. `\\PC\share\manifest.json`
  or `D:\...\manifest.json`) instead of an HTTPS URL — handy for LAN servers or testing.
- Patch filenames must match what the WoW client expects in `Data\` (e.g. `patch-4.MPQ`,
  `patch-A.MPQ`). The client loads numbered patches, then lettered ones (later overrides earlier).
- The launcher writes `realmlist.wtf` into every locale folder it finds under `Data\`.
