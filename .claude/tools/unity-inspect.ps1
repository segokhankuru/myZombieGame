<#
.SYNOPSIS
  Summarizes a Unity scene, prefab or asset without putting its YAML in context.

.DESCRIPTION
  A .unity file is routinely 10k-50k lines. Reading one costs more tokens than an entire
  sprint. This script streams the YAML, reconstructs the hierarchy, resolves MonoBehaviour
  script GUIDs to class names, and prints a summary of 20-40 lines.

  ASCII-only, Windows PowerShell 5.1 compatible, no external modules.

.EXAMPLE
  .\.claude\tools\unity-inspect.ps1 -Path Assets/_Project/Scenes/Levels/Level_01.unity
  .\.claude\tools\unity-inspect.ps1 -Path Assets/_Project/Prefabs/Player.prefab -Depth 4
  .\.claude\tools\unity-inspect.ps1 -FindReferences Assets/_Project/Prefabs/Crate.prefab
  .\.claude\tools\unity-inspect.ps1 -RebuildGuidMap
#>
[CmdletBinding()]
param(
    [string]$Path,
    [int]$Depth = 3,
    [int]$MaxNodes = 60,
    [string]$FindReferences,
    [switch]$RebuildGuidMap,
    [string]$ProjectRoot = "."
)

$ErrorActionPreference = 'Stop'

# Invariant culture is not optional here. On a tr-TR machine the default case-insensitive
# -match folds 'I' to dotless 'i', which breaks every [A-Za-z] class, and numbers format
# with a comma decimal separator. Both produce silently wrong output.
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture

$root = (Resolve-Path $ProjectRoot).Path
$stateDir = Join-Path $root ".state"
$guidMapPath = Join-Path $stateDir "guid-map.json"

# ---------------------------------------------------------------- class id table
$ClassNames = @{
    '1'='GameObject'; '2'='Component'; '4'='Transform'; '8'='Behaviour'; '20'='Camera';
    '21'='Material'; '23'='MeshRenderer'; '25'='Renderer'; '28'='Texture2D'; '33'='MeshFilter';
    '43'='Mesh'; '45'='Skybox'; '47'='QualitySettings'; '54'='Rigidbody'; '55'='PhysicsManager';
    '56'='Collider'; '58'='CircleCollider2D'; '59'='HingeJoint'; '64'='MeshCollider';
    '65'='BoxCollider'; '68'='EdgeCollider2D'; '70'='CapsuleCollider2D'; '72'='ComputeShader';
    '74'='AnimationClip'; '81'='AudioListener'; '82'='AudioSource'; '83'='AudioClip';
    '84'='RenderTexture'; '95'='Animator'; '96'='TrailRenderer'; '102'='TextMesh';
    '104'='RenderSettings'; '108'='Light'; '111'='Animation'; '114'='MonoBehaviour';
    '115'='MonoScript'; '120'='LineRenderer'; '124'='Flare'; '128'='Font';
    '135'='SphereCollider'; '136'='CapsuleCollider'; '137'='SkinnedMeshRenderer';
    '143'='CharacterController'; '145'='StreamingController'; '147'='ResourceManager';
    '152'='MovieTexture'; '154'='TerrainCollider'; '156'='TerrainData'; '157'='LightmapSettings';
    '158'='WebCamTexture'; '159'='EditorSettings'; '162'='EditorUserSettings';
    '164'='AudioReverbFilter'; '165'='AudioHighPassFilter'; '166'='AudioChorusFilter';
    '167'='AudioReverbZone'; '168'='AudioEchoFilter'; '169'='AudioLowPassFilter';
    '170'='AudioDistortionFilter'; '180'='AudioBehaviour'; '181'='AudioFilter';
    '182'='WindZone'; '183'='Cloth'; '191'='OffMeshLink'; '192'='OcclusionArea';
    '193'='Tree'; '195'='NavMeshAgent'; '196'='NavMeshSettings'; '198'='ParticleSystem';
    '199'='ParticleSystemRenderer'; '205'='LODGroup'; '212'='SpriteRenderer';
    '213'='Sprite'; '218'='Terrain'; '220'='LightProbeGroup'; '221'='AnimatorOverrideController';
    '222'='CanvasRenderer'; '223'='Canvas'; '224'='RectTransform'; '225'='CanvasGroup';
    '226'='BillboardAsset'; '227'='BillboardRenderer'; '229'='AudioMixer';
    '238'='NavMeshObstacle'; '240'='AudioMixerSnapshot'; '241'='AudioMixerGroup';
    '244'='NavMeshData'; '245'='AudioMixerController'; '246'='AudioMixerGroupController';
    '258'='LightProbes'; '290'='AssetBundleManifest'; '331'='SpriteMask';
    '363'='OcclusionCullingData'; '850595691'='LightingSettings';
    '1001'='PrefabInstance'; '1002'='EditorExtensionImpl'; '1003'='AssetImporter';
    '1660057539'='SceneRoots'; '1839735485'='SceneVisibilityState';
}

function Get-ClassName([string]$id) {
    if ($ClassNames.ContainsKey($id)) { return $ClassNames[$id] }
    return "Class$id"
}

# ------------------------------------------------------------------- guid map
function Build-GuidMap {
    $map = @{}
    $assets = Join-Path $root "Assets"
    if (-not (Test-Path $assets)) { return $map }
    $metas = Get-ChildItem -LiteralPath $assets -Recurse -Filter "*.cs.meta" -File -ErrorAction SilentlyContinue
    foreach ($m in $metas) {
        $line = Get-Content -LiteralPath $m.FullName -TotalCount 3 -ErrorAction SilentlyContinue |
                Where-Object { $_ -match '^guid:\s*([0-9a-f]{32})' }
        if ($line -and $Matches[1]) {
            $map[$Matches[1]] = [IO.Path]::GetFileNameWithoutExtension($m.BaseName)
        }
    }
    if (-not (Test-Path $stateDir)) { New-Item -ItemType Directory -Path $stateDir | Out-Null }
    ($map | ConvertTo-Json -Compress -Depth 3) | Out-File -LiteralPath $guidMapPath -Encoding utf8
    return $map
}

function Get-GuidMap {
    if ($RebuildGuidMap -or -not (Test-Path $guidMapPath)) { return Build-GuidMap }
    try {
        $o = Get-Content -LiteralPath $guidMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $h = @{}
        foreach ($p in $o.PSObject.Properties) { $h[$p.Name] = $p.Value }
        return $h
    } catch { return Build-GuidMap }
}

# --------------------------------------------------------------- find references
if ($FindReferences) {
    $needle = $FindReferences
    if (Test-Path $needle) {
        $meta = "$needle.meta"
        if (Test-Path $meta) {
            $g = Get-Content -LiteralPath $meta -TotalCount 3 | Where-Object { $_ -match 'guid:\s*([0-9a-f]{32})' }
            if ($Matches[1]) { $needle = $Matches[1] }
        }
    }
    if ($needle -notmatch '^[0-9a-f]{32}$') {
        Write-Output "ERROR: could not resolve '$FindReferences' to a GUID. Pass a 32-char guid or an asset path with a .meta file."
        exit 1
    }
    Write-Output "REFERENCES TO guid $needle"
    $hits = Get-ChildItem -LiteralPath (Join-Path $root "Assets") -Recurse -File `
              -Include *.unity,*.prefab,*.asset,*.mat,*.controller -ErrorAction SilentlyContinue |
            Select-String -SimpleMatch -Pattern $needle -List -ErrorAction SilentlyContinue
    if (-not $hits) { Write-Output "  none"; exit 0 }
    foreach ($h in $hits) {
        $rel = $h.Path.Substring($root.Length).TrimStart('\','/')
        Write-Output ("  " + $rel)
    }
    Write-Output ("TOTAL " + @($hits).Count + " file(s)")
    exit 0
}

if (-not $Path) { Write-Output "ERROR: -Path or -FindReferences required."; exit 1 }
if (-not (Test-Path $Path)) { Write-Output "ERROR: not found: $Path"; exit 1 }

$full = (Resolve-Path $Path).Path
$fi = Get-Item -LiteralPath $full
$rel = $full
if ($full.StartsWith($root)) { $rel = $full.Substring($root.Length).TrimStart('\','/') }

# --------------------------------------------------------------------- parse
$guidMap = Get-GuidMap

$gameObjects = @{}   # anchor -> @{ Name; Components=@() ; Transform }
$transforms  = @{}   # anchor -> @{ Go; Father; Children=@() }
$pending     = $null # current document
$curClass    = $null
$curAnchor   = $null
$section     = ''
$lineCount   = 0
$scriptCounts = @{}
$prefabInstances = 0
$componentCount  = 0

function Commit-Doc {
    param($cls,$anchor,$data)
    if (-not $anchor) { return }
    switch ($cls) {
        '1' {
            $script:gameObjects[$anchor] = [ordered]@{
                Name = $data.Name; Components = $data.CompRefs; Active = $data.Active
            }
        }
        '4'   { $script:transforms[$anchor] = @{ Go=$data.Go; Father=$data.Father; Children=$data.Children } }
        '224' { $script:transforms[$anchor] = @{ Go=$data.Go; Father=$data.Father; Children=$data.Children } }
        '1001'{ $script:prefabInstances++ }
    }
}

$sr = New-Object System.IO.StreamReader($full)
try {
    $data = $null
    while (($line = $sr.ReadLine()) -ne $null) {
        $lineCount++
        if ($line.StartsWith('--- !u!')) {
            if ($data) { Commit-Doc $curClass $curAnchor $data }
            $curClass = $null; $curAnchor = $null; $section = ''
            if ($line -match '^--- !u!(\d+)\s+&(\d+)') { $curClass = $Matches[1]; $curAnchor = $Matches[2] }
            $data = @{ Name=''; CompRefs=@(); Go=''; Father=''; Children=@(); Active=1; ScriptGuid='' }
            continue
        }
        if (-not $curClass) { continue }

        switch ($curClass) {
            '1' {
                if     ($line -match '^\s{2}m_Name:\s*(.*)$')        { $data.Name = $Matches[1].Trim() }
                elseif ($line -match '^\s{2}m_IsActive:\s*(\d)')     { $data.Active = [int]$Matches[1] }
                elseif ($line -match '^\s{2}m_Component:')           { $section = 'comp' }
                elseif ($section -eq 'comp' -and $line -match 'component:\s*\{fileID:\s*(\d+)\}') {
                    $data.CompRefs += $Matches[1]
                }
                elseif ($line -match '^\s{2}\w') { $section = '' }
            }
            { $_ -eq '4' -or $_ -eq '224' } {
                if     ($line -match '^\s{2}m_GameObject:\s*\{fileID:\s*(\d+)\}') { $data.Go = $Matches[1] }
                elseif ($line -match '^\s{2}m_Father:\s*\{fileID:\s*(\d+)\}')     { $data.Father = $Matches[1] }
                elseif ($line -match '^\s{2}m_Children:')                          { $section = 'kids' }
                elseif ($section -eq 'kids' -and $line -match 'fileID:\s*(\d+)')   { $data.Children += $Matches[1] }
                elseif ($line -match '^\s{2}\w') { $section = '' }
            }
            '114' {
                if ($line -match '^\s{2}m_Script:.*guid:\s*([0-9a-f]{32})') {
                    $g = $Matches[1]
                    $n = if ($guidMap.ContainsKey($g)) { $guidMap[$g] } else { "script:" + $g.Substring(0,8) }
                    if ($scriptCounts.ContainsKey($n)) { $scriptCounts[$n]++ } else { $scriptCounts[$n] = 1 }
                    $data.ScriptGuid = $n
                }
                if ($line -match '^\s{2}m_GameObject:\s*\{fileID:\s*(\d+)\}') { $data.Go = $Matches[1] }
            }
        }
        if ($curClass -ne '1' -and $curClass -ne '1001') { }
    }
    if ($data) { Commit-Doc $curClass $curAnchor $data }
} finally { $sr.Close() }

# ----------------------------------------------------------------- hierarchy
foreach ($k in $gameObjects.Keys) { $componentCount += @($gameObjects[$k].Components).Count }

# transform anchor -> gameobject anchor, and reverse
$goToTransform = @{}
foreach ($t in $transforms.Keys) {
    $go = $transforms[$t].Go
    if ($go) { $goToTransform[$go] = $t }
}

# Roots, ordered by weight: a scene with 3000 roots is usually one meaningful container
# plus a flood of loose prefab instances. Show the containers that carry the scene first.
$rootList = @()
foreach ($t in $transforms.Keys) {
    $f = $transforms[$t].Father
    if ($f -and $f -ne '0') { continue }
    $go = $transforms[$t].Go
    if (-not $go -or -not $gameObjects.ContainsKey($go)) { continue }
    $rootList += [pscustomobject]@{
        Anchor   = $t
        Kids     = @($transforms[$t].Children).Count
        Name     = $gameObjects[$go].Name
    }
}
$roots = @($rootList | Sort-Object -Property @{Expression='Kids';Descending=$true}, 'Name' |
           Select-Object -ExpandProperty Anchor)

function Get-CompSummary($goAnchor) {
    $names = @()
    foreach ($c in @($gameObjects[$goAnchor].Components)) {
        # component anchor -> we did not store class per anchor for non-GO docs; approximate
        $names += $c
    }
    return $names.Count
}

$emitted = 0
$linesOut = New-Object System.Collections.Generic.List[string]

function Emit-Node($tAnchor, $level) {
    if ($script:emitted -ge $MaxNodes) { return }
    if ($level -gt $Depth) { return }
    $go = $transforms[$tAnchor].Go
    if (-not $go -or -not $gameObjects.ContainsKey($go)) { return }
    $name = $gameObjects[$go].Name
    if (-not $name) { $name = '(unnamed)' }
    $kids = @($transforms[$tAnchor].Children)
    $nc = @($gameObjects[$go].Components).Count
    $suffix = ""
    if ($nc -gt 1) { $suffix += " [$nc comps]" }
    if ($kids.Count -gt 0) { $suffix += " ($($kids.Count) children)" }
    if ($gameObjects[$go].Active -eq 0) { $suffix += " [INACTIVE]" }
    $script:linesOut.Add(("  " * ($level+1)) + $name + $suffix)
    $script:emitted++
    if ($level -lt $Depth) {
        foreach ($k in $kids) { if ($transforms.ContainsKey($k)) { Emit-Node $k ($level+1) } }
    }
}

foreach ($r in $roots) { Emit-Node $r 0 }

# --------------------------------------------------------------------- output
$kind = switch -Wildcard ($fi.Extension) {
    '.unity'  { 'SCENE' }
    '.prefab' { 'PREFAB' }
    '.asset'  { 'ASSET' }
    default   { 'FILE' }
}
$sizeKb = [math]::Round($fi.Length / 1KB, 1)

Write-Output "$kind $rel"
Write-Output ("  {0} lines, {1} KB" -f $lineCount, $sizeKb)
Write-Output ("  GameObjects {0} | Components {1} | Prefab instances {2} | Distinct scripts {3}" -f `
    $gameObjects.Count, $componentCount, $prefabInstances, $scriptCounts.Count)
Write-Output ""
Write-Output "HIERARCHY (depth $Depth, $($roots.Count) roots, heaviest first)"
if ($linesOut.Count -eq 0) { Write-Output "  (no transform hierarchy - not a scene or prefab)" }
foreach ($l in $linesOut) { Write-Output $l }
if ($emitted -ge $MaxNodes) {
    Write-Output "  ... truncated at $MaxNodes nodes. Use -MaxNodes or -Depth to widen."
}

if ($scriptCounts.Count -gt 0) {
    Write-Output ""
    Write-Output "SCRIPTS"
    $scriptCounts.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 20 |
        ForEach-Object { Write-Output ("  {0,-40} x{1}" -f $_.Key, $_.Value) }
    if ($scriptCounts.Count -gt 20) { Write-Output ("  ... and " + ($scriptCounts.Count - 20) + " more") }
}

# heaviest branches
$heavy = @()
foreach ($t in $transforms.Keys) {
    $n = @($transforms[$t].Children).Count
    if ($n -ge 15) {
        $go = $transforms[$t].Go
        if ($go -and $gameObjects.ContainsKey($go)) {
            $heavy += [pscustomobject]@{ Name = $gameObjects[$go].Name; Count = $n }
        }
    }
}
if ($heavy.Count -gt 0) {
    Write-Output ""
    Write-Output "HEAVY BRANCHES (15+ direct children)"
    $heavy | Sort-Object Count -Descending | Select-Object -First 8 |
        ForEach-Object { Write-Output ("  {0,-40} {1} children" -f $_.Name, $_.Count) }
}
