#requires -Version 7.0
param([string]$SkinsDirectory = (Join-Path $PSScriptRoot '../CodexBall.App/Skins'))
$ErrorActionPreference = 'Stop'
function Get-ReferencePaths($node) {
    if ($node -is [string]) { $node }
    elseif ($node -is [System.Collections.IDictionary]) {
        foreach ($key in $node.Keys) {
            # Expression Name and motion metadata are labels, not asset paths.
            if ($key -in @('Moc', 'Textures', 'Physics', 'Pose', 'UserData', 'DisplayInfo', 'Expressions', 'Motions', 'File', 'Sound') -or
                $node[$key] -is [System.Collections.IDictionary] -or
                $node[$key] -is [array]) { Get-ReferencePaths $node[$key] }
        }
    } elseif ($node -is [System.Collections.IEnumerable]) {
        foreach ($value in $node) { Get-ReferencePaths $value }
    }
}
foreach ($id in @('local-koharu', 'local-haruto', 'local-mao_pro', 'local-mark_free_t04')) {
    $pack = Join-Path $SkinsDirectory $id
    $manifest = Get-Content -LiteralPath (Join-Path $pack 'manifest.json') -Raw | ConvertFrom-Json
    if (!$manifest.live2DModel) { throw "Missing Live2D model in $id manifest." }
    $modelFile = Join-Path $pack $manifest.live2DModel
    $modelRoot = [IO.Path]::GetFullPath((Split-Path $modelFile))
    $definition = Get-Content -LiteralPath $modelFile -Raw | ConvertFrom-Json -AsHashtable
    if (!$definition.FileReferences.Moc -or !$definition.FileReferences.Textures.Count) {
        throw "Missing model or texture references in $modelFile"
    }
    foreach ($reference in @(Get-ReferencePaths $definition.FileReferences)) {
        $path = [IO.Path]::GetFullPath((Join-Path $modelRoot $reference))
        if (!$path.StartsWith($modelRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Out-of-folder model reference: $reference"
        }
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
            throw "Missing or empty model asset: $path"
        }
    }
    if (!(Test-Path -LiteralPath (Join-Path $pack 'Source-ReadMe.txt') -PathType Leaf)) {
        throw "Missing source ReadMe for $id"
    }
    Write-Host "Validated $id and all referenced runtime assets."
}
