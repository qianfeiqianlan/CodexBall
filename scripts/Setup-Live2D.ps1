param([string]$SdkArchive)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdkVersion = '5-r.4.1'
$expectedHash = 'B71EC6BAFC6578CCE3B4CBBAA42A1CB51AE6EB477557932B02D22AF957E733C7'
$cacheDirectory = Join-Path $projectRoot 'artifacts/live2d'
New-Item -ItemType Directory -Force -Path $cacheDirectory | Out-Null
if (!$SdkArchive) {
    $SdkArchive = Join-Path $cacheDirectory "CubismSdkForNative-$sdkVersion.zip"
    if (!(Test-Path -LiteralPath $SdkArchive)) {
        Write-Host 'Downloading the official Live2D SDK. SDK and sample licenses: https://www.live2d.com/en/sdk/download/native/'
        Invoke-WebRequest "https://cubism.live2d.com/sdk-native/bin/CubismSdkForNative-$sdkVersion.zip" -OutFile $SdkArchive
    }
}
if ((Get-FileHash -LiteralPath $SdkArchive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Unexpected SDK archive hash. Use the official Cubism SDK for Native 5 R4.1 archive.'
}
$sdkRoot = Join-Path $cacheDirectory "CubismSdkForNative-$sdkVersion"
Expand-Archive -LiteralPath $SdkArchive -DestinationPath $cacheDirectory -Force
$runtimeDirectory = Join-Path $projectRoot 'CodexBall.App/Live2D'
$licenseDirectory = Join-Path $runtimeDirectory 'Licenses'
New-Item -ItemType Directory -Force -Path $runtimeDirectory,$licenseDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $sdkRoot 'Core/dll/windows/x86_64/Live2DCubismCore.dll') -Destination $runtimeDirectory -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'Core/LICENSE.md') -Destination (Join-Path $licenseDirectory 'CubismCore-LICENSE.md') -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'LICENSE.md') -Destination (Join-Path $licenseDirectory 'CubismSDK-LICENSE.md') -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'Core/RedistributableFiles.txt') -Destination $licenseDirectory -Force
Write-Host 'Live2D runtime is ready. Prepare bundled models with Import-Live2DModels.ps1, then build CodexBall.'
