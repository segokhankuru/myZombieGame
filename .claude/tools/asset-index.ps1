<#
.SYNOPSIS
  Inventory of the Unity project's assets, with size budgets. Replaces recursive listing.

.EXAMPLE
  .\.claude\tools\asset-index.ps1
  .\.claude\tools\asset-index.ps1 -Scope Assets/_Project/Art -Top 20
  .\.claude\tools\asset-index.ps1 -Heavy          # only what is over budget
  .\.claude\tools\asset-index.ps1 -Scripts        # C# surface only
#>
[CmdletBinding()]
param(
    [string]$Scope = "Assets",
    [int]$Top = 12,
    [switch]$Heavy,
    [switch]$Scripts,
    [string]$ProjectRoot = "."
)

$ErrorActionPreference = 'SilentlyContinue'

# Invariant culture is not optional here. On a tr-TR machine the default case-insensitive
# -match folds 'I' to dotless 'i', which breaks every [A-Za-z] class, and numbers format
# with a comma decimal separator. Both produce silently wrong output.
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path
$scopePath = Join-Path $root $Scope
if (-not (Test-Path $scopePath)) { $scopePath = $Scope }
if (-not (Test-Path $scopePath)) { Write-Output "ERROR: scope not found: $Scope"; exit 1 }

# Budget thresholds. Over these, a file is called out. Tuned for a PC indie title.
$Budget = @{
    '.png' = 4MB; '.jpg' = 3MB; '.tga' = 8MB; '.psd' = 40MB; '.exr' = 24MB
    '.fbx' = 12MB; '.obj' = 12MB; '.blend' = 40MB
    '.wav' = 12MB; '.mp3' = 8MB; '.ogg' = 8MB
    '.unity' = 3MB; '.prefab' = 1MB; '.asset' = 4MB; '.cs' = 120KB
}

$Categories = @{
    'Textures'  = @('.png','.jpg','.jpeg','.tga','.psd','.exr','.hdr','.tif')
    'Models'    = @('.fbx','.obj','.blend','.dae','.gltf','.glb')
    'Audio'     = @('.wav','.mp3','.ogg','.aiff')
    'Scenes'    = @('.unity')
    'Prefabs'   = @('.prefab')
    'Assets(SO)'= @('.asset')
    'Materials' = @('.mat')
    'Shaders'   = @('.shader','.shadergraph','.hlsl','.cginc','.compute')
    'Anim'      = @('.anim','.controller','.overrideController')
    'Code'      = @('.cs')
    'Config'    = @('.json','.xml','.csv','.yaml')
    'Fonts'     = @('.ttf','.otf','.fontsettings')
    'Video'     = @('.mp4','.webm','.mov')
}
$extToCat = @{}
foreach ($c in $Categories.Keys) { foreach ($e in $Categories[$c]) { $extToCat[$e] = $c } }

Write-Output "ASSET INDEX  $Scope"

$files = Get-ChildItem -LiteralPath $scopePath -Recurse -File |
         Where-Object { $_.Extension -ne '.meta' -and $_.FullName -notmatch '\\(Library|Temp|obj|Logs|Build)\\' }

if (-not $files) { Write-Output "  (empty)"; exit 0 }

# ------------------------------------------------------------------- scripts view
if ($Scripts) {
    $cs = $files | Where-Object { $_.Extension -eq '.cs' }
    $totalLines = 0
    $byDir = @{}
    foreach ($f in $cs) {
        $n = 0
        try { $n = [Linq.Enumerable]::Count([IO.File]::ReadLines($f.FullName)) } catch {}
        $totalLines += $n
        $d = Split-Path $f.FullName -Parent
        $d = $d.Substring($root.Length).TrimStart('\','/')
        if ($byDir.ContainsKey($d)) { $byDir[$d] += $n } else { $byDir[$d] = $n }
    }
    Write-Output ("  {0} C# files, {1} lines total" -f $cs.Count, $totalLines)
    Write-Output ""
    Write-Output "LARGEST NAMESPACES BY LINES"
    $byDir.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top |
        ForEach-Object { Write-Output ("  {0,-58} {1,7} lines" -f $_.Key, $_.Value) }
    Write-Output ""
    Write-Output "LARGEST FILES"
    $cs | Sort-Object Length -Descending | Select-Object -First 8 | ForEach-Object {
        Write-Output ("  {0,-58} {1,6} KB" -f $_.FullName.Substring($root.Length).TrimStart('\','/'), [math]::Round($_.Length/1KB))
    }
    $asmdefs = Get-ChildItem -LiteralPath $scopePath -Recurse -Filter *.asmdef -File
    Write-Output ""
    Write-Output ("ASSEMBLIES ({0})" -f @($asmdefs).Count)
    foreach ($a in $asmdefs) { Write-Output ("  " + $a.BaseName) }
    if (@($asmdefs).Count -eq 0) {
        Write-Output "  none - every script is in Assembly-CSharp. Full recompile on every edit."
        Write-Output "  Fix: /architecture, then split into asmdefs per unity-conventions.md."
    }
    exit 0
}

# --------------------------------------------------------------- summary by category
$rows = @()
$grand = 0
foreach ($cat in ($Categories.Keys | Sort-Object)) {
    $exts = $Categories[$cat]
    $sel = $files | Where-Object { $exts -contains $_.Extension.ToLower() }
    if (-not $sel) { continue }
    $sum = ($sel | Measure-Object Length -Sum).Sum
    $grand += $sum
    $rows += [pscustomobject]@{ Category=$cat; Count=@($sel).Count; MB=[math]::Round($sum/1MB,1) }
}
$other = $files | Where-Object { -not $extToCat.ContainsKey($_.Extension.ToLower()) }
if ($other) {
    $sum = ($other | Measure-Object Length -Sum).Sum
    $grand += $sum
    $rows += [pscustomobject]@{ Category='Other'; Count=@($other).Count; MB=[math]::Round($sum/1MB,1) }
}

Write-Output ("  {0} files, {1} MB total" -f $files.Count, [math]::Round($grand/1MB,1))
Write-Output ""
if (-not $Heavy) {
    Write-Output "BY CATEGORY"
    $rows | Sort-Object MB -Descending | ForEach-Object {
        Write-Output ("  {0,-12} {1,6} files  {2,8} MB" -f $_.Category, $_.Count, $_.MB)
    }
    Write-Output ""
}

# ---------------------------------------------------------------- over budget
$over = @()
foreach ($f in $files) {
    $e = $f.Extension.ToLower()
    if ($Budget.ContainsKey($e) -and $f.Length -gt $Budget[$e]) {
        $over += [pscustomobject]@{
            Path = $f.FullName.Substring($root.Length).TrimStart('\','/')
            MB   = [math]::Round($f.Length/1MB,1)
            Cap  = [math]::Round($Budget[$e]/1MB,1)
        }
    }
}
Write-Output ("OVER BUDGET ({0})" -f $over.Count)
if ($over.Count -eq 0) {
    Write-Output "  none"
} else {
    $over | Sort-Object MB -Descending | Select-Object -First $Top | ForEach-Object {
        Write-Output ("  {0,8} MB (cap {1}) {2}" -f $_.MB, $_.Cap, $_.Path)
    }
    if ($over.Count -gt $Top) { Write-Output ("  ... and " + ($over.Count - $Top) + " more") }
}

if ($Heavy) { exit 0 }

# ---------------------------------------------------------------- top level map
Write-Output ""
Write-Output "TOP-LEVEL FOLDERS"
Get-ChildItem -LiteralPath $scopePath -Directory | ForEach-Object {
    $d = $_
    $sub = Get-ChildItem -LiteralPath $d.FullName -Recurse -File | Where-Object { $_.Extension -ne '.meta' }
    $sz = ($sub | Measure-Object Length -Sum).Sum
    [pscustomobject]@{ Name=$d.Name; Files=@($sub).Count; MB=[math]::Round($sz/1MB,1) }
} | Sort-Object MB -Descending | Select-Object -First $Top | ForEach-Object {
    Write-Output ("  {0,-44} {1,6} files {2,8} MB" -f $_.Name, $_.Files, $_.MB)
}

# ---------------------------------------------------------------- naming check
# -cmatch, not -match: see the culture note at the top of this file.
$badNames = $files | Where-Object {
    $_.Extension -in @('.png','.jpg','.tga','.wav','.ogg','.fbx','.prefab','.mat') -and
    ($_.BaseName -cmatch '[^A-Za-z0-9_. -]' -or $_.BaseName -cmatch '\s{2,}')
} | Select-Object -First 6
if ($badNames) {
    Write-Output ""
    Write-Output "NAMING (non-ASCII or double spaces - see unity-conventions.md section 5)"
    foreach ($b in $badNames) { Write-Output ("  " + $b.Name) }
}
