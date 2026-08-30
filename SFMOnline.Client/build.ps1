$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Resolve-Path $MyInvocation.MyCommand.Path)
$game = $env:SFM_GAME_DIR
if (-not $game) { $game = Split-Path -Parent $root }
$outDir = Join-Path $root 'bin'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sdkRootEnv = $env:SFM_SDK_DIR
$csc = $null
if ($sdkRootEnv) {
    $csc = Join-Path $sdkRootEnv ('sdk\8.0.424\Roslyn\bincore\csc.dll')
}
if (-not $csc -or -not (Test-Path $csc)) {
    $csc = 'C:\Program Files\dotnet\sdk\8.0.423\Roslyn\bincore\csc.dll'
}
if (-not (Test-Path $csc)) {
    $sdkRoot = Join-Path (Split-Path (Get-Command dotnet).Source -Parent) 'sdk'
    $sdkVer = Get-ChildItem $sdkRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $csc = Join-Path $sdkVer.FullName 'Roslyn\bincore\csc.dll'
}

$netStdRefs = 'C:\Program Files\dotnet\packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1'
if (-not (Test-Path $netStdRefs) -and $sdkRootEnv) {
    $netStdRefs = Join-Path $sdkRootEnv 'packs\NETStandard.Library.Ref\2.1.0\ref\netstandard2.1'
}
if (-not (Test-Path $netStdRefs)) { throw "缺少 NETStandard 引用包: $netStdRefs" }

$cscArgs = @(
    '-nologo',
    '-noconfig',
    '-nostdlib+',
    '-target:library',
    '-langversion:latest',
    '-nullable:disable',
    '-warn:0',
    ('-out:' + (Join-Path $outDir 'SFMOnline.dll'))
)

Get-ChildItem $netStdRefs -Filter *.dll | ForEach-Object { $cscArgs += ('-r:' + $_.FullName) }

$gameRefs = @(
    'BepInEx\core\BepInEx.Core.dll',
    'BepInEx\core\BepInEx.Unity.IL2CPP.dll',
    'BepInEx\core\Il2CppInterop.Runtime.dll',
    'BepInEx\interop\Assembly-CSharp.dll',
    'BepInEx\interop\Il2Cppmscorlib.dll',
    'BepInEx\interop\UnityEngine.CoreModule.dll',
    'BepInEx\interop\UnityEngine.ImageConversionModule.dll',
    'BepInEx\interop\UnityEngine.AnimationModule.dll',
    'BepInEx\interop\UnityEngine.IMGUIModule.dll',
    'BepInEx\interop\UnityEngine.UI.dll',
    'BepInEx\interop\UnityEngine.UIModule.dll',
    'BepInEx\interop\UnityEngine.InputLegacyModule.dll',
    'BepInEx\interop\UnityEngine.PhysicsModule.dll',
    'BepInEx\interop\UnityEngine.ParticleSystemModule.dll',
    'BepInEx\interop\UnityEngine.AIModule.dll',
    'BepInEx\interop\UnityEngine.TextRenderingModule.dll'
)
foreach ($r in $gameRefs) {
    $p = Join-Path $game $r
    if (-not (Test-Path $p)) { throw "缺少引用: $p" }
    $cscArgs += ('-r:' + $p)
}

Get-ChildItem $root -Filter *.cs -Recurse | ForEach-Object { $cscArgs += $_.FullName }

$dotnetCmd = 'dotnet'
if ($sdkRootEnv) { $dotnetCmd = Join-Path $sdkRootEnv 'dotnet.exe' }
if (-not (Test-Path $dotnetCmd)) { $dotnetCmd = 'dotnet' }

& $dotnetCmd $csc @cscArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ''
Write-Host "构建完成: $outDir\SFMOnline.dll"
