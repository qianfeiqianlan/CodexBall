param([string]$SdkArchive)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$expectedHash = 'B71EC6BAFC6578CCE3B4CBBAA42A1CB51AE6EB477557932B02D22AF957E733C7'
$cache = Join-Path $projectRoot 'artifacts/live2d'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
if (!$SdkArchive) {
    $SdkArchive = Join-Path $cache 'CubismSdkForNative-5-r.4.1.zip'
    if (!(Test-Path -LiteralPath $SdkArchive)) {
        Write-Host 'Downloading pinned Native 5 R4.1. Licenses: https://www.live2d.com/en/sdk/download/native/'
        Invoke-WebRequest 'https://cubism.live2d.com/sdk-native/bin/CubismSdkForNative-5-r.4.1.zip' -OutFile $SdkArchive
    }
}
if ((Get-FileHash -LiteralPath $SdkArchive -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Unexpected SDK archive hash.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $SdkArchive).Path)
try {
    $files = @{
        'Core/dll/windows/x86_64/Live2DCubismCore.dll' = 'runtimes/win-x64/native/Live2DCubismCore.dll'
        'Core/dll/linux/x86_64/libLive2DCubismCore.so' = 'runtimes/linux-x64/native/libLive2DCubismCore.so'
        'Core/dll/macos/libLive2DCubismCore.dylib' = 'runtimes/osx/native/libLive2DCubismCore.dylib'
        'Core/LICENSE.md' = 'Licenses/CubismCore-LICENSE.md'
        'LICENSE.md' = 'Licenses/CubismSDK-LICENSE.md'
        'Core/RedistributableFiles.txt' = 'Licenses/RedistributableFiles.txt'
    }
    foreach ($relative in $files.Keys) {
        $entry = $archive.GetEntry("CubismSdkForNative-5-r.4.1/$relative")
        if (!$entry) { throw "SDK entry missing: $relative" }
        $target = Join-Path $projectRoot "CodexBall.Rendering/Native/$($files[$relative])"
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
    }
}
finally { $archive.Dispose() }
Write-Host 'Probe native runtimes prepared: win-x64, linux-x64, macOS universal x64/arm64. Run Import-Live2DModels.ps1 for Koharu.'
