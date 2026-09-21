<#
  Make-Manifest.ps1
  Scans a folder of patch MPQs and builds manifest.json (with MD5 + size for each),
  so publishing an update is one command.

  EXAMPLE:
    .\Make-Manifest.ps1 `
        -PatchFolder "D:\Wow Private\Custom patches" `
        -BaseUrl "https://github.com/USER/REPO/releases/download/patches" `
        -Template "D:\Wow Private\Launcher\manifest.sample.json" `
        -Output "D:\Wow Private\Launcher\manifest.json"

  Then: commit manifest.json to your GitHub repo, and upload each MPQ as a release asset
  under the tag in the BaseUrl (here the "patches" release). See README.md.
#>
param(
    [Parameter(Mandatory=$true)] [string]$PatchFolder,
    [Parameter(Mandatory=$true)] [string]$BaseUrl,
    [string]$Template = "",
    [string]$Output = "manifest.json",
    [string]$Filter = "*.MPQ",
    # Files that must land in the GAME ROOT (next to Wow.exe), not under Data\.
    # e.g. the patched Wow.exe and AwesomeWotlkLib.dll. Pass full paths.
    [string[]]$RootFiles = @()
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

if (-not (Test-Path $PatchFolder)) { throw "Patch folder not found: $PatchFolder" }

# Start from the template (keeps serverName/realmlist/news/etc.) or a minimal default
if ($Template -and (Test-Path $Template)) {
    $manifest = Get-Content $Template -Raw | ConvertFrom-Json
} else {
    $manifest = [pscustomobject]@{
        serverName  = "My WoW Server"; realmlist = "127.0.0.1"
        statusHost  = "127.0.0.1";     statusPort = 3724
        registerUrl = ""; websiteUrl = ""; news = @(); patches = @()
    }
}

# Locale codes WoW uses; a patch whose name contains one goes in Data\<locale>\ (higher load priority),
# everything else goes in Data\ (global). e.g. patch-enUS-4.MPQ -> "enUS",  patch-9.MPQ -> ""
$locales = 'enUS','enGB','deDE','frFR','esES','esMX','ruRU','koKR','zhCN','zhTW','ptBR','itIT'

$patches = @()
Get-ChildItem -Path $PatchFolder -Filter $Filter -File | Sort-Object Name | ForEach-Object {
    $dest = ""
    foreach ($loc in $locales) { if ($_.Name -match $loc) { $dest = $loc; break } }
    $where = if ($dest) { "Data\$dest\" } else { "Data\" }
    Write-Host ("Hashing {0} ({1:N1} MB) -> {2}" -f $_.Name, ($_.Length/1MB), $where)
    $md5 = (Get-FileHash -Path $_.FullName -Algorithm MD5).Hash.ToLower()
    $patches += [pscustomobject]@{
        file = $_.Name
        dest = $dest
        root = $false
        url  = "$BaseUrl/$($_.Name)"
        md5  = $md5
        size = $_.Length
    }
}

# Root files: placed next to Wow.exe (game root), not under Data\.
foreach ($rf in $RootFiles) {
    if (-not (Test-Path $rf)) { throw "Root file not found: $rf" }
    $item = Get-Item $rf
    Write-Host ("Hashing {0} ({1:N1} MB) -> game root" -f $item.Name, ($item.Length/1MB))
    $md5 = (Get-FileHash -Path $item.FullName -Algorithm MD5).Hash.ToLower()
    $patches += [pscustomobject]@{
        file = $item.Name
        dest = ""
        root = $true
        url  = "$BaseUrl/$($item.Name)"
        md5  = $md5
        size = $item.Length
    }
}

$manifest.patches = $patches
$manifest | ConvertTo-Json -Depth 6 | Out-File -FilePath $Output -Encoding utf8
Write-Host ""
Write-Host ("Wrote {0} with {1} patch entr{2}." -f $Output, $patches.Count, $(if($patches.Count -eq 1){'y'}else{'ies'})) -ForegroundColor Green
