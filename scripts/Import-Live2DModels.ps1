#requires -Version 7.0
param([string]$SourceDirectory = (Join-Path $PSScriptRoot '../tmp'))
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -AssemblyName System.IO.Compression.FileSystem
$displayNames = @{
    koharu = '小春 Koharu · Q版'
    haruto = '春翔 Haruto · Q版'
    mao_pro = '虹色 Mao'
    mark_free_t04 = '马克君 Mark'
}
foreach ($archive in Get-ChildItem -LiteralPath $SourceDirectory -Filter '*.zip' | Sort-Object Name) {
    $extractRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/import-models/' + $archive.BaseName)))
    $zip = [IO.Compression.ZipFile]::OpenRead($archive.FullName)
    try {
        foreach ($entry in $zip.Entries) {
            $target = [IO.Path]::GetFullPath((Join-Path $extractRoot $entry.FullName))
            if (!$target.StartsWith($extractRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Archive contains an out-of-folder entry: $($entry.FullName)"
            }
        }
    } finally { $zip.Dispose() }
    Expand-Archive -LiteralPath $archive.FullName -DestinationPath $extractRoot -Force
    $models = @(Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter '*.model3.json')
    if ($models.Count -eq 0) {
        Write-Warning "$($archive.Name): editor-only files, no exported model3.json/moc3. Skipped."
        continue
    }
    foreach ($model in $models) {
        $name = $model.Name -replace '\.model3\.json$', ''
        $id = 'local-' + ($name -replace '[^a-zA-Z0-9_-]', '-').ToLowerInvariant()
        $packRoot = Join-Path $projectRoot "CodexBall.App/Skins/$id"
        $modelRoot = Join-Path $packRoot 'model'
        New-Item -ItemType Directory -Force -Path $modelRoot | Out-Null
        Copy-Item -Path (Join-Path $model.Directory.FullName '*') -Destination $modelRoot -Recurse -Force
        $readme = Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter 'ReadMe.txt' | Select-Object -First 1
        if ($readme) { Copy-Item -LiteralPath $readme.FullName -Destination (Join-Path $packRoot 'Source-ReadMe.txt') -Force }
        # Curated manifests contain application-specific layout and action settings.
        # Refresh all runtime assets, but never overwrite an existing manifest.
        if (Test-Path -LiteralPath (Join-Path $packRoot 'manifest.json')) {
            Write-Host "Imported $name -> $id (preserved manifest.json)"
            continue
        }
        $definition = Get-Content -LiteralPath $model.FullName -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
        $groups = @($definition.FileReferences.Motions.Keys)
        $tapGroup = 'Idle'
        if ($groups -contains 'TapBody') { $tapGroup = 'TapBody' }
        elseif ($groups -contains 'Tap') { $tapGroup = 'Tap' }
        elseif ($groups -contains '') { $tapGroup = '' }
        $isChibi = $name -in @('koharu', 'haruto')
        $height = if ($isChibi) { 260 } else { 320 }
        $manifest = [ordered]@{
            schemaVersion = 1; id = $id
            name = $(if ($displayNames.ContainsKey($name)) { $displayNames[$name] } else { $name })
            author = 'Live2D Inc. (user-downloaded official sample)'
            license = 'Live2D Free Material License; see Source-ReadMe.txt and THIRD-PARTY-NOTICES.md'
            width = 240; height = $height
            head = @{ x = 35; y = 10; width = 170; height = $(if ($isChibi) { 140 } else { 115 }) }
            orb = @{ x = 196; y = ($height - 70); width = 38; height = 38 }
            live2DModel = ('model/' + $model.Name)
            motionGroups = @{ Idle = 'Idle'; Pat = $tapGroup; QuotaRecovered = $tapGroup }
        }
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $packRoot 'manifest.json') -Encoding UTF8
        Write-Host "Imported $name -> $id (click motion group: '$tapGroup')"
    }
}
Write-Host 'Build CodexBall, then choose a model from the Change Appearance menu.'
