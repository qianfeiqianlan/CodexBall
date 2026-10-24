#requires -Version 7.0
param([string]$DestinationDirectory = (Join-Path $PSScriptRoot '../artifacts/model-downloads'))
$ErrorActionPreference = 'Stop'
# Official Cubism sample downloads. Keep these hashes pinned when updating assets.
$archives = @(
    @{ Path = 'koharu_haruto/koharu_haruto_ja.zip'; Hash = 'A0F677361EA94D8266E3C195ECDC6251E9E49529174315AF0640E8EB5D170594' },
    @{ Path = 'mao/mao_zh-Hans.zip'; Hash = 'FAB30A0CAECD65DE3EE297AC90E068DFFD23AD6A331BF7B628244109725BEEF6' },
    @{ Path = 'mark/mark_zh-Hans.zip'; Hash = '2D2322FC6AC99FC4E42163566AC4D32E0B77B05F4C06BDD32009B48BC12B867B' }
)
New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
foreach ($archive in $archives) {
    $target = Join-Path $DestinationDirectory ([IO.Path]::GetFileName($archive.Path))
    if (!(Test-Path -LiteralPath $target)) {
        Invoke-WebRequest -Uri ('https://cubism.live2d.com/sample-data/bin/' + $archive.Path) -OutFile $target
    }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $archive.Hash) {
        throw "Unexpected model archive hash: $target. Remove the cached file and verify the official download before updating its pinned hash."
    }
    Write-Host "Verified $target"
}
