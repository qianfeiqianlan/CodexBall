param(
    [ValidateSet('win-x64','osx-arm64','osx-x64','linux-x64')][string]$Runtime = 'win-x64',
    [string]$Version,
    [switch]$SkipZip
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (!$Version) { $Version = ([xml](Get-Content (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version }
$outputRoot = Join-Path $projectRoot 'artifacts/desktop'
$destination = Join-Path $outputRoot $Runtime
$mac = $Runtime.StartsWith('osx-')
if ($mac) { $destination = Join-Path $destination 'CodexBall.app/Contents/MacOS' }
New-Item -ItemType Directory -Force -Path $destination | Out-Null
& dotnet publish (Join-Path $projectRoot 'CodexBall.Desktop/CodexBall.Desktop.csproj') -c Release -r $Runtime --self-contained true "-p:Version=$Version" -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed' }
if (!(Test-Path -LiteralPath (Join-Path $destination 'Skins/local-koharu/model/koharu.model3.json'))) { throw 'Prepare the curated models before packaging: scripts/Import-Live2DModels.ps1' }
$native = if ($Runtime -eq 'win-x64') { 'runtimes/win-x64/native/Live2DCubismCore.dll' } elseif ($mac) { 'runtimes/osx/native/libLive2DCubismCore.dylib' } else { 'runtimes/linux-x64/native/libLive2DCubismCore.so' }
if (!(Test-Path -LiteralPath (Join-Path $destination $native))) { throw 'Prepare the native runtimes first: scripts/Setup-Desktop.ps1' }
if ($mac) {
    $contents = Split-Path -Parent $destination
    $packageRoot = Join-Path $outputRoot $Runtime
    # A Windows cross-publish still needs signing on the target Mac. Ship the
    # installer beside the bundle so this local validation package is usable.
    $installerText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Install-CodexBall.command')).Replace("`r`n", "`n")
    [IO.File]::WriteAllText((Join-Path $packageRoot 'Install-CodexBall.command'), $installerText, [Text.UTF8Encoding]::new($false))
    $installNotes = @'
CodexBall Mac 本地验证包

1. 退出旧版 CodexBall。
2. 解压完整 ZIP，保留 CodexBall.app 和 Install-CodexBall.command 在同一目录。
3. 在终端进入解压目录，运行：bash ./Install-CodexBall.command

脚本会在这台 Mac 上为本地构建生成 ad-hoc 签名，验证通过后安装并打开应用。
优先安装到 /Applications；该目录不可写时使用 ~/Applications，不需要 sudo。
已有应用会先备份，设置不会删除；签名失败时不替换旧应用。
程序运行在菜单栏，不显示 Dock 图标；小球随 Codex 活动显示。

这是本地验证签名，不是 Apple Developer ID 签名或公证。
'@
    [IO.File]::WriteAllText((Join-Path $packageRoot 'INSTALL-MAC.txt'), $installNotes.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.codexball.desktop</string>
<key>CFBundleName</key><string>CodexBall</string>
<key>CFBundleDisplayName</key><string>CodexBall</string>
<key>CFBundleExecutable</key><string>CodexBall.Desktop</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$Version</string>
<key>CFBundleVersion</key><string>$Version</string>
<key>LSUIElement</key><true/>
<key>NSHighResolutionCapable</key><true/>
<key>LSMinimumSystemVersion</key><string>12.0</string>
</dict></plist>
"@
    [IO.File]::WriteAllText((Join-Path $contents 'Info.plist'), $plist, [Text.UTF8Encoding]::new($false))
    if ($IsMacOS) {
        & chmod +x (Join-Path $destination 'CodexBall.Desktop')
        if ($LASTEXITCODE -ne 0) { throw 'Unable to set executable permission' }
        # Cross-publishing on Windows cannot use Apple's codesign tool. On macOS,
        # seal native code inside-out and sign the bundle last for local execution.
        $appBundle = Split-Path -Parent $contents
        foreach ($candidate in Get-ChildItem -LiteralPath $destination -Recurse -File) {
            $description = & /usr/bin/file -b $candidate.FullName
            if ($LASTEXITCODE -ne 0) { throw "Unable to inspect native file: $($candidate.FullName)" }
            if ($description -notmatch 'Mach-O') { continue }
            & /usr/bin/codesign --force --sign - $candidate.FullName
            if ($LASTEXITCODE -ne 0) { throw "Unable to sign native file: $($candidate.FullName)" }
        }
        & /usr/bin/codesign --force --sign - $appBundle
        if ($LASTEXITCODE -ne 0) { throw 'Unable to sign app bundle' }
        & /usr/bin/codesign --verify --deep --strict --verbose=2 $appBundle
        if ($LASTEXITCODE -ne 0) { throw 'App signature verification failed' }
    } else {
        Write-Warning 'Cross-published Mac apphost is not signed. Before local execution, ad-hoc sign the apphost and bundle on the target Mac. For release packages, build and verify signatures on macOS.'
    }
}
if (!$SkipZip) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archivePath = Join-Path $outputRoot "CodexBall-Desktop-$Version-$Runtime.zip"
    # FileMode.Create replaces only this explicitly named local build artifact.
    $zipFile = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
    $zip = [IO.Compression.ZipArchive]::new($zipFile, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $root = Join-Path $outputRoot $Runtime
        foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
            $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace([char]92,[char]47)
            $entry = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $relative, [IO.Compression.CompressionLevel]::Fastest)
            if (!$Runtime.StartsWith('win-')) {
                $mode = if ($file.Name -eq 'CodexBall.Desktop' -or $file.Extension -in '.dylib','.so','.command') { 33261 } else { 33188 }
                $entry.ExternalAttributes = $mode -shl 16
            }
        }
    }
    finally { $zip.Dispose(); $zipFile.Dispose() }
    if (!$Runtime.StartsWith('win-')) {
        # ZipArchive on Windows writes a DOS creator id. Mark central-directory records
        # as Unix so macOS unzip honors the executable mode recorded above.
        $stream = [IO.File]::Open($archivePath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite)
        try {
            $tailSize = [int][Math]::Min(65557, $stream.Length)
            $tail = [byte[]]::new($tailSize)
            $stream.Position = $stream.Length - $tailSize
            $stream.ReadExactly($tail)
            $end = -1
            for ($i = $tailSize - 22; $i -ge 0; $i--) {
                if ([BitConverter]::ToUInt32($tail,$i) -eq 0x06054b50) { $end=$i; break }
            }
            if ($end -lt 0) { throw 'ZIP directory not found' }
            $count = [BitConverter]::ToUInt16($tail,$end+10)
            $offset = [long][BitConverter]::ToUInt32($tail,$end+16)
            $header = [byte[]]::new(46)
            for ($i=0; $i -lt $count; $i++) {
                $stream.Position=$offset
                $stream.ReadExactly($header)
                if ([BitConverter]::ToUInt32($header,0) -ne 0x02014b50) { throw 'Invalid ZIP directory entry' }
                $stream.Position=$offset+5
                $stream.WriteByte(3)
                $offset += 46 + [BitConverter]::ToUInt16($header,28) + [BitConverter]::ToUInt16($header,30) + [BitConverter]::ToUInt16($header,32)
            }
        }
        finally { $stream.Dispose() }
    }
    Write-Output "Packaged: $archivePath"
}
