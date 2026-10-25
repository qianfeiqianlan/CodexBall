param([string]$SdkArchive)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Setup-AvaloniaProbe.ps1') -SdkArchive $SdkArchive
