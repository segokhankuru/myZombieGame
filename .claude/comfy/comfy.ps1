<#
.SYNOPSIS
  The studio's local asset generation client. Agents call this; agents never touch a
  ComfyUI workflow graph.

.DESCRIPTION
  A ComfyUI API workflow is several hundred lines of node wiring. Putting one in an
  agent's context is the most expensive mistake available in this repository. This
  script owns the graphs, the model discovery, the style lock, the queueing, the
  polling, the download and the naming, and returns at most a few lines.

  Windows PowerShell 5.1, no modules, no Python.

.EXAMPLE
  .\.claude\comfy\comfy.ps1 status
  .\.claude\comfy\comfy.ps1 caps -Refresh
  .\.claude\comfy\comfy.ps1 gen -Prompt "wooden shipping crate, three quarter view" -Slug tex_crate_wood_01 -Count 4
  .\.claude\comfy\comfy.ps1 gen -Workflow sprite -Prompt "hammer icon" -Slug ui_icon_hammer
  .\.claude\comfy\comfy.ps1 vary -Source Assets/_Project/Art/Generated/props/tex_crate_wood_01_b.png -Count 4
  .\.claude\comfy\comfy.ps1 upscale -Source <path> -Factor 2
  .\.claude\comfy\comfy.ps1 jobs
#>
[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [ValidateSet('status','caps','gen','vary','upscale','jobs','cancel','styles')]
    [string]$Command = 'status',

    [string]$Prompt,
    [string]$Negative,
    [string]$Slug,
    [string]$Out,
    [int]$Count = 4,
    [string]$Workflow = 'txt2img',
    [string]$Source,
    [double]$Strength = 0.35,
    [int]$Factor = 2,
    [int]$Seed = 0,
    [int]$Width = 0,
    [int]$Height = 0,
    [int]$Steps = 0,
    [double]$Cfg = 0,
    [string]$Style = 'project',
    [string]$JobId,

    [string]$Server = 'http://127.0.0.1:8188',
    [switch]$Refresh,
    [switch]$NoWait,
    [int]$TimeoutSeconds = 900,
    [string]$ProjectRoot = "."
)

$ErrorActionPreference = 'Stop'
[Threading.Thread]::CurrentThread.CurrentCulture   = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root      = (Resolve-Path $ProjectRoot).Path
$comfyDir  = Join-Path $root ".claude\comfy"
if (-not (Test-Path $comfyDir)) { $comfyDir = Join-Path $PSScriptRoot "." }
$comfyDir  = (Resolve-Path $comfyDir).Path
$wfDir     = Join-Path $comfyDir "workflows"
$styleDir  = Join-Path $comfyDir "styles"
$stateDir  = Join-Path $root ".state"
$capsPath  = Join-Path $stateDir "comfy-caps.json"
$jobsPath  = Join-Path $stateDir "comfy-jobs.jsonl"
if (-not (Test-Path $stateDir)) { New-Item -ItemType Directory -Path $stateDir -Force | Out-Null }

$clientId = "unity-studio-" + $PID

# ===================================================================== helpers

function Invoke-Comfy {
    param([string]$Path, [string]$Method = 'GET', $Body, [int]$TimeoutSec = 20)
    $uri = $Server.TrimEnd('/') + $Path
    try {
        if ($Method -eq 'POST') {
            $json = $Body | ConvertTo-Json -Depth 30 -Compress
            $bytes = [Text.Encoding]::UTF8.GetBytes($json)
            return Invoke-RestMethod -Uri $uri -Method Post -Body $bytes -ContentType 'application/json' -TimeoutSec $TimeoutSec
        }
        return Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $TimeoutSec
    } catch {
        throw ("ComfyUI request failed ({0} {1}): {2}" -f $Method, $Path, $_.Exception.Message)
    }
}

function Test-Server {
    try { $null = Invoke-Comfy -Path '/system_stats' -TimeoutSec 5; return $true } catch { return $false }
}

function Write-Down {
    Write-Output "COMFY: server not reachable at $Server"
    Write-Output "  Start ComfyUI Desktop (or 'python main.py --listen 127.0.0.1 --port 8188')"
    Write-Output "  and re-run. Nothing was queued."
    Write-Output "  This studio never starts ComfyUI for you: GPU work is your call, not the agent's."
}

function Get-Style {
    $p = Join-Path $styleDir "$Style.json"
    if (-not (Test-Path $p)) {
        if ($Style -eq 'project') {
            throw "No locked style at .claude/comfy/styles/project.json. Run /art-direction first - generating without a style lock produces images that do not belong to the same game."
        }
        throw "Style '$Style' not found in .claude/comfy/styles/."
    }
    return (Get-Content -LiteralPath $p -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Get-Caps {
    if (-not $Refresh -and (Test-Path $capsPath)) {
        try { return (Get-Content -LiteralPath $capsPath -Raw -Encoding UTF8 | ConvertFrom-Json) } catch {}
    }
    if (-not (Test-Server)) { Write-Down; exit 2 }

    $info = Invoke-Comfy -Path '/object_info' -TimeoutSec 60

    function OptionsOf($nodeName, $inputName) {
        $n = $info.PSObject.Properties[$nodeName]
        if (-not $n) { return @() }
        $req = $n.Value.input.required
        if (-not $req) { return @() }
        $f = $req.PSObject.Properties[$inputName]
        if (-not $f) { return @() }
        $v = @($f.Value)[0]
        if ($v -is [Array]) { return @($v) }
        return @()
    }

    $caps = [ordered]@{
        server           = $Server
        refreshed        = (Get-Date -Format 'yyyy-MM-dd HH:mm')
        nodeCount        = @($info.PSObject.Properties).Count
        checkpoints      = OptionsOf 'CheckpointLoaderSimple' 'ckpt_name'
        diffusionModels  = OptionsOf 'UNETLoader' 'unet_name'
        clip             = OptionsOf 'CLIPLoader' 'clip_name'
        vae              = OptionsOf 'VAELoader' 'vae_name'
        loras            = OptionsOf 'LoraLoader' 'lora_name'
        upscalers        = OptionsOf 'UpscaleModelLoader' 'model_name'
        controlnets      = OptionsOf 'ControlNetLoader' 'control_net_name'
        samplers         = OptionsOf 'KSampler' 'sampler_name'
        schedulers       = OptionsOf 'KSampler' 'scheduler'
        hasBackgroundRemoval = [bool]($info.PSObject.Properties['ImageRemoveBackground'] -or $info.PSObject.Properties['RemBGSession+'] -or $info.PSObject.Properties['InspyrenetRembg'])
    }
    ($caps | ConvertTo-Json -Depth 6) | Out-File -LiteralPath $capsPath -Encoding utf8
    return ($caps | ConvertTo-Json -Depth 6 | ConvertFrom-Json)
}

function Get-DeterministicSeed {
    param([string]$key, [int]$index, [long]$family)
    $md5 = [Security.Cryptography.MD5]::Create()
    $h = $md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("$key#$index"))
    $n = [BitConverter]::ToUInt32($h, 0) % 100000000
    return [int](($family + $n) % 2147483647)
}

function Resolve-Workflow {
    param($caps, [string]$name)
    # An explicit custom graph always wins: drop an API-format export at
    # .claude/comfy/workflows/custom.json and it is used as-is.
    $custom = Join-Path $wfDir "custom.json"
    if (Test-Path $custom) { return @{ Path = $custom; Family = 'custom' } }

    $hasCkpt = @($caps.checkpoints).Count -gt 0
    $hasUnet = @($caps.diffusionModels).Count -gt 0

    $family = if ($hasCkpt) { 'sd' } elseif ($hasUnet) { 'flux' } else { $null }
    if (-not $family) {
        throw "No usable models found. ComfyUI reports no checkpoints and no diffusion models. Install one, then run: comfy.ps1 caps -Refresh"
    }
    $file = switch ($name) {
        'txt2img'  { "$family-txt2img.json" }
        'sprite'   { "$family-txt2img.json" }
        'tileable' { "$family-txt2img.json" }
        'img2img'  { "$family-img2img.json" }
        'upscale'  { "upscale.json" }
        default    { "$name.json" }
    }
    $p = Join-Path $wfDir $file
    if (-not (Test-Path $p)) {
        throw "Workflow template not found: $file`n  Export an API-format workflow from ComfyUI (Settings > Enable dev mode > Save (API Format)) and save it as .claude/comfy/workflows/custom.json with {{PROMPT}} / {{SEED}} / {{WIDTH}} / {{HEIGHT}} / {{BATCH}} placeholders."
    }
    return @{ Path = $p; Family = $family }
}

function Expand-Template {
    param([string]$text, [hashtable]$vars)
    foreach ($k in $vars.Keys) {
        $text = $text.Replace("{{$k}}", [string]$vars[$k])
    }
    if ($text -cmatch '\{\{([A-Z_]+)\}\}') {
        throw "Workflow template still has an unfilled placeholder: {{$($Matches[1])}}"
    }
    return $text
}

function Test-GraphNodes {
    param($graph)
    $info = $null
    try { $info = Invoke-Comfy -Path '/object_info' -TimeoutSec 60 } catch { return }
    $missing = @()
    foreach ($p in $graph.PSObject.Properties) {
        $ct = $p.Value.class_type
        if ($ct -and -not $info.PSObject.Properties[$ct]) { $missing += $ct }
    }
    if ($missing.Count -gt 0) {
        throw ("This ComfyUI install does not have these node types: {0}`n  Install the custom node pack that provides them, or replace the template with your own API export at .claude/comfy/workflows/custom.json" -f (($missing | Select-Object -Unique) -join ', '))
    }
}

function Add-JobLine($obj) {
    ($obj | ConvertTo-Json -Compress -Depth 6) | Add-Content -LiteralPath $jobsPath -Encoding utf8
}

function Save-Outputs {
    param([string]$promptId, [string]$outDir, [string]$slug)
    $hist = Invoke-Comfy -Path "/history/$promptId" -TimeoutSec 30
    $entry = $hist.PSObject.Properties[$promptId]
    if (-not $entry) { return @() }
    $saved = @()
    $letters = 'abcdefghijklmnopqrstuvwxyz'
    $i = 0
    foreach ($nodeOut in $entry.Value.outputs.PSObject.Properties) {
        foreach ($img in @($nodeOut.Value.images)) {
            if (-not $img.filename) { continue }
            $q = "/view?filename=$([uri]::EscapeDataString($img.filename))&subfolder=$([uri]::EscapeDataString([string]$img.subfolder))&type=$([string]$img.type)"
            $ext = [IO.Path]::GetExtension($img.filename)
            if (-not $ext) { $ext = '.png' }
            $suffix = if ($i -lt $letters.Length) { $letters[$i] } else { "$i" }
            $dest = Join-Path $outDir ("{0}_{1}{2}" -f $slug, $suffix, $ext)
            Invoke-WebRequest -Uri ($Server.TrimEnd('/') + $q) -OutFile $dest -TimeoutSec 120 | Out-Null
            $saved += $dest
            $i++
        }
    }
    return $saved
}

# ===================================================================== commands

switch ($Command) {

'status' {
    if (-not (Test-Server)) { Write-Down; exit 2 }
    $st = Invoke-Comfy -Path '/system_stats'
    $q  = Invoke-Comfy -Path '/queue'
    Write-Output "COMFY OK  $Server"
    if ($st.system) {
        Write-Output ("  {0}  python {1}  comfy {2}" -f $st.system.os, $st.system.python_version.Split(' ')[0], $st.system.comfyui_version)
    }
    foreach ($d in @($st.devices)) {
        $freeGb  = [math]::Round($d.vram_free / 1GB, 1)
        $totalGb = [math]::Round($d.vram_total / 1GB, 1)
        Write-Output ("  {0}  VRAM {1} / {2} GB free" -f $d.name, $freeGb, $totalGb)
    }
    Write-Output ("  queue: {0} running, {1} pending" -f @($q.queue_running).Count, @($q.queue_pending).Count)
    $stylePath = Join-Path $styleDir "project.json"
    if (Test-Path $stylePath) {
        $s = Get-Content -LiteralPath $stylePath -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Output ("  style lock: '{0}' on {1}" -f $s.name, $s.model)
    } else {
        Write-Output "  style lock: NONE - run /art-direction before generating anything."
    }
    exit 0
}

'caps' {
    $caps = Get-Caps
    Write-Output ("COMFY CAPS  refreshed {0}  ({1} node types)" -f $caps.refreshed, $caps.nodeCount)
    function ShowList($label, $arr, $max = 8) {
        $a = @($arr)
        if ($a.Count -eq 0) { Write-Output ("  {0,-18} none" -f $label); return }
        Write-Output ("  {0,-18} {1}" -f $label, ($a | Select-Object -First $max) -join ', ')
        if ($a.Count -gt $max) { Write-Output ("  {0,-18} ... and {1} more" -f '', ($a.Count - $max)) }
    }
    ShowList 'checkpoints'      $caps.checkpoints
    ShowList 'diffusion models' $caps.diffusionModels
    ShowList 'text encoders'    $caps.clip
    ShowList 'vae'              $caps.vae
    ShowList 'loras'            $caps.loras
    ShowList 'upscalers'        $caps.upscalers
    ShowList 'controlnets'      $caps.controlnets
    Write-Output ("  {0,-18} {1}" -f 'bg removal', $caps.hasBackgroundRemoval)
    Write-Output ""
    if (@($caps.checkpoints).Count -eq 0 -and @($caps.diffusionModels).Count -eq 0) {
        Write-Output "  No image model installed. /art-direction cannot lock a style yet."
    } else {
        Write-Output "  Pick the model in /art-direction. Never hardcode a filename in a skill."
    }
    exit 0
}

'styles' {
    Write-Output "STYLES  .claude/comfy/styles/"
    Get-ChildItem -LiteralPath $styleDir -Filter *.json -File | ForEach-Object {
        $s = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $lock = if ($_.BaseName -eq 'project') { ' [LOCKED]' } else { '' }
        Write-Output ("  {0,-16}{1}  {2}" -f $_.BaseName, $lock, $s.name)
        Write-Output ("      model {0}  steps {1}  cfg {2}" -f $s.model, $s.steps, $s.cfg)
    }
    exit 0
}

'jobs' {
    if (-not (Test-Path $jobsPath)) { Write-Output "No generation history yet."; exit 0 }
    Write-Output "GENERATION HISTORY (most recent last)"
    Get-Content -LiteralPath $jobsPath -Tail 15 | ForEach-Object {
        try {
            $j = $_ | ConvertFrom-Json
            Write-Output ("  {0}  {1,-10} {2,-28} {3} img  seed {4}" -f $j.ts, $j.workflow, $j.slug, $j.count, $j.seed)
        } catch {}
    }
    if (Test-Server) {
        $q = Invoke-Comfy -Path '/queue'
        Write-Output ("QUEUE NOW: {0} running, {1} pending" -f @($q.queue_running).Count, @($q.queue_pending).Count)
    }
    exit 0
}

'cancel' {
    if (-not (Test-Server)) { Write-Down; exit 2 }
    $null = Invoke-Comfy -Path '/interrupt' -Method POST -Body @{}
    Write-Output "COMFY: interrupted the running job. Pending items remain queued."
    exit 0
}

{ $_ -in @('gen','vary','upscale') } {

    if (-not (Test-Server)) { Write-Down; exit 2 }
    $caps  = Get-Caps
    $style = Get-Style

    if ($Command -ne 'upscale' -and -not $Prompt) { Write-Output "ERROR: -Prompt is required."; exit 1 }
    if (-not $Slug) {
        if ($Source) { $Slug = [IO.Path]::GetFileNameWithoutExtension($Source) + "_v" }
        else { Write-Output "ERROR: -Slug is required (it becomes the file name and the provenance key)."; exit 1 }
    }
    if ($Slug -cnotmatch '^[a-z0-9_]+$') {
        Write-Output "ERROR: -Slug must be lowercase letters, digits and underscores (unity-conventions.md section 5). Got '$Slug'."
        exit 1
    }

    if (-not $Out) { $Out = Join-Path $root "Assets\_Project\Art\Generated" }
    if (-not (Test-Path $Out)) { New-Item -ItemType Directory -Path $Out -Force | Out-Null }

    # ---- resolve size from the style's per-use-case table
    $sizeKey = switch ($Workflow) { 'sprite' { 'sprite' } 'tileable' { 'tileable' } default { 'default' } }
    $dims = $style.size.PSObject.Properties[$sizeKey]
    if (-not $dims) { $dims = $style.size.PSObject.Properties['default'] }
    $w = if ($Width  -gt 0) { $Width }  else { [int]@($dims.Value)[0] }
    $h = if ($Height -gt 0) { $Height } else { [int]@($dims.Value)[1] }

    $steps = if ($Steps -gt 0) { $Steps } else { [int]$style.steps }
    $cfg   = if ($Cfg   -gt 0) { $Cfg }   else { [double]$style.cfg }

    $family = [long]$style.seedFamily
    $seed = if ($Seed -gt 0) { $Seed } else { Get-DeterministicSeed -key $Slug -index 0 -family $family }

    # ---- prompt assembly: the caller supplies subject, the lock supplies style
    $positive = ($style.positive + ", " + $Prompt).Trim(', ')
    $negative = if ($Negative) { $style.negative + ", " + $Negative } else { [string]$style.negative }
    if ($Workflow -eq 'tileable') { $positive = "seamless tileable texture, top down, even lighting, " + $positive }
    if ($Workflow -eq 'sprite')   { $positive = "single object, centered, plain flat background, " + $positive }

    $wf = if ($Command -eq 'upscale') { Resolve-Workflow $caps 'upscale' }
          elseif ($Command -eq 'vary') { Resolve-Workflow $caps 'img2img' }
          else { Resolve-Workflow $caps $Workflow }

    $model = [string]$style.model
    if ($wf.Family -eq 'sd' -and @($caps.checkpoints) -notcontains $model) {
        Write-Output "ERROR: the locked style names checkpoint '$model' but this ComfyUI does not have it."
        Write-Output ("  Available: " + ((@($caps.checkpoints) | Select-Object -First 10) -join ', '))
        Write-Output "  Fix the lock via /art-direction. Never silently substitute a model - it changes the look of the game."
        exit 1
    }
    if ($wf.Family -eq 'flux' -and @($caps.diffusionModels) -notcontains $model) {
        Write-Output "ERROR: the locked style names diffusion model '$model' but this ComfyUI does not have it."
        Write-Output ("  Available: " + ((@($caps.diffusionModels) | Select-Object -First 10) -join ', '))
        exit 1
    }

    $vars = @{
        PROMPT      = ($positive -replace '"','\"')
        NEGATIVE    = ($negative -replace '"','\"')
        SEED        = $seed
        STEPS       = $steps
        CFG         = $cfg
        SAMPLER     = [string]$style.sampler
        SCHEDULER   = [string]$style.scheduler
        WIDTH       = $w
        HEIGHT      = $h
        BATCH       = $Count
        MODEL       = $model
        CLIP        = [string]$style.clip
        CLIP2       = [string]$style.clip2
        CLIPTYPE    = $(if ($style.clipType) { [string]$style.clipType } else { 'flux' })
        VAE         = [string]$style.vae
        DENOISE     = $(if ($Command -eq 'vary') { $Strength } else { 1.0 })
        PREFIX      = $Slug
        UPSCALER    = $(if (@($caps.upscalers).Count -gt 0) { @($caps.upscalers)[0] } else { '' })
        SOURCE      = $(if ($Source) { (Split-Path $Source -Leaf) } else { '' })
    }

    $raw = Get-Content -LiteralPath $wf.Path -Raw -Encoding UTF8
    $filled = Expand-Template $raw $vars
    $graph = $null
    try { $graph = $filled | ConvertFrom-Json }
    catch { Write-Output "ERROR: workflow template produced invalid JSON after substitution: $($wf.Path)"; exit 1 }

    Test-GraphNodes $graph

    if ($Command -eq 'vary' -or $Command -eq 'upscale') {
        if (-not $Source -or -not (Test-Path $Source)) { Write-Output "ERROR: -Source image required and must exist."; exit 1 }
        Write-Output "NOTE: the source image must already be in the ComfyUI input folder."
        Write-Output ("  Copy it there first, or use the ComfyUI UI. Looking for: " + (Split-Path $Source -Leaf))
    }

    $resp = Invoke-Comfy -Path '/prompt' -Method POST -Body @{ prompt = $graph; client_id = $clientId }
    $pid_ = $resp.prompt_id
    if (-not $pid_) { Write-Output "ERROR: ComfyUI did not return a prompt id."; exit 1 }
    $short = $pid_.Substring(0, 8)

    Add-JobLine ([ordered]@{
        ts = (Get-Date -Format 'yyyy-MM-dd HH:mm'); promptId = $pid_; workflow = $Workflow
        slug = $Slug; count = $Count; seed = $seed; style = $style.name; model = $model; out = $Out
    })

    if ($NoWait) {
        Write-Output ("QUEUED {0}  {1}  {2}  {3} img  seed {4}" -f $short, $Workflow, $Slug, $Count, $seed)
        Write-Output "  Do other work. Collect with: comfy.ps1 jobs"
        exit 0
    }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    $done = $false
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        Start-Sleep -Milliseconds 1500
        $hist = $null
        try { $hist = Invoke-Comfy -Path "/history/$pid_" -TimeoutSec 20 } catch { continue }
        if ($hist.PSObject.Properties[$pid_]) { $done = $true; break }
    }
    $sw.Stop()

    if (-not $done) {
        Write-Output ("TIMEOUT {0} after {1}s. The job may still be running." -f $short, $TimeoutSeconds)
        Write-Output "  Check with: comfy.ps1 jobs"
        exit 3
    }

    $saved = Save-Outputs -promptId $pid_ -outDir $Out -slug $Slug
    Write-Output ("JOB {0}  {1}  {2}  {3} image(s)  seed {4}  {5:N1}s" -f `
        $short, $Workflow, $Slug, $saved.Count, $seed, $sw.Elapsed.TotalSeconds)
    foreach ($s in $saved) {
        $rel = $s
        if ($s.StartsWith($root)) { $rel = $s.Substring($root.Length).TrimStart('\','/') }
        Write-Output ("  " + $rel)
    }
    if ($saved.Count -eq 0) {
        Write-Output "  No images returned. The graph ran but produced no SaveImage output."
    } else {
        Write-Output "  Next: pick one, then promote it via /gen-asset (cleanup, naming, budget, ASSET-LOG)."
    }
    exit 0
}

}
