# Unity Studio - PreToolUse guard
# Blocks the single most expensive mistake in a Unity repo: reading a scene, prefab or
# Editor log into context. One 15 MB scene can cost more tokens than an entire sprint.
# Exit 2 = block the call and show this text to the model.
# ASCII-only, Windows PowerShell 5.1.

$ErrorActionPreference = 'SilentlyContinue'
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture

$raw = [Console]::In.ReadToEnd()
if (-not $raw) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$tool = [string]$payload.tool_name
$path = [string]$payload.tool_input.file_path
if (-not $path) { $path = [string]$payload.tool_input.path }
if (-not $path) { exit 0 }

$name = Split-Path $path -Leaf
$ext  = [IO.Path]::GetExtension($path).ToLower()
$norm = $path -replace '\\', '/'

$sizeKb = 0
if (Test-Path -LiteralPath $path) {
    $sizeKb = [int]((Get-Item -LiteralPath $path).Length / 1KB)
}

# ---------------------------------------------------------------- reads
if ($tool -eq 'Read') {

    if ($ext -eq '.unity' -or $ext -eq '.prefab') {
        $kind = if ($ext -eq '.unity') { 'scene' } else { 'prefab' }
        [Console]::Error.WriteLine("BLOCKED: this is a Unity $kind ($sizeKb KB of machine-generated YAML).")
        [Console]::Error.WriteLine("Reading it wastes a large fraction of the context window and tells you less than the tool does.")
        [Console]::Error.WriteLine("")
        [Console]::Error.WriteLine("Use instead:")
        [Console]::Error.WriteLine("  .claude/tools/unity-inspect.ps1 -Path `"$path`"")
        [Console]::Error.WriteLine("")
        [Console]::Error.WriteLine("If you need to know who references an asset:")
        [Console]::Error.WriteLine("  .claude/tools/unity-inspect.ps1 -FindReferences <path-or-guid>")
        [Console]::Error.WriteLine("See .claude/docs/unity-conventions.md section 2.")
        exit 2
    }

    if ($ext -eq '.meta') {
        [Console]::Error.WriteLine("BLOCKED: .meta files are import bookkeeping and almost never carry the answer.")
        [Console]::Error.WriteLine("If you need the GUID: .claude/tools/unity-inspect.ps1 -FindReferences `"$($path -replace '\.meta$','')`"")
        exit 2
    }

    if ($ext -eq '.asset' -and $sizeKb -gt 256) {
        [Console]::Error.WriteLine("BLOCKED: $name is a $sizeKb KB Unity asset (usually terrain, lightmap or navmesh data).")
        [Console]::Error.WriteLine("Use: .claude/tools/unity-inspect.ps1 -Path `"$path`"")
        exit 2
    }

    if ($name -cmatch '^(Editor|Editor-prev|build|upm)\.log$' -or ($ext -eq '.log' -and $sizeKb -gt 512)) {
        [Console]::Error.WriteLine("BLOCKED: $name is a $sizeKb KB log. Reading it is never the cheapest way to the answer.")
        [Console]::Error.WriteLine("Use: .claude/tools/unity-log.ps1 -Errors")
        exit 2
    }

    if ($norm -cmatch '/(Library|Temp|obj|Build)/') {
        [Console]::Error.WriteLine("BLOCKED: $norm is build output, not source. Nothing in Library/, Temp/, obj/ or Build/ is authoritative.")
        exit 2
    }

    if ($sizeKb -gt 2048) {
        [Console]::Error.WriteLine("BLOCKED: $name is $sizeKb KB. Read a targeted range with offset/limit, or Grep for what you need.")
        exit 2
    }
}

# ---------------------------------------------------------------- writes
if ($tool -eq 'Write' -or $tool -eq 'Edit') {

    if ($ext -eq '.unity' -or $ext -eq '.prefab' -or $ext -eq '.meta') {
        $kind = switch ($ext) { '.unity' {'scene'} '.prefab' {'prefab'} default {'meta file'} }
        [Console]::Error.WriteLine("BLOCKED: hand-editing a Unity $kind corrupts fileID and GUID consistency in ways that surface days later.")
        [Console]::Error.WriteLine("")
        [Console]::Error.WriteLine("Author it through an editor script instead:")
        [Console]::Error.WriteLine("  Assets/_Project/Code/Editor/  (owner: tools-programmer)")
        [Console]::Error.WriteLine("Or state in the story that this step needs manual Editor work by the user.")
        [Console]::Error.WriteLine("See .claude/docs/unity-conventions.md section 2.")
        exit 2
    }

    if ($norm -cmatch '/Assets/_Project/Config/.*\.asset$') {
        [Console]::Error.WriteLine("BLOCKED: Assets/_Project/Config/ holds generated ScriptableObjects.")
        [Console]::Error.WriteLine("Tuning is edited in config/balance/*.json and imported. Editing the .asset makes JSON and engine disagree.")
        [Console]::Error.WriteLine("See .claude/docs/config-protocol.md.")
        exit 2
    }

    if ($norm -cmatch '/\.state/') {
        [Console]::Error.WriteLine("BLOCKED: .state/ is machine state owned by /status and the hooks. Do not hand-edit it.")
        exit 2
    }
}

exit 0
