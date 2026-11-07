# PlayOut Co-Op (GPL-3): builds PlayOutCoop.exe and everything it needs into .\dist\ (one flat folder).
#
#   powershell -ExecutionPolicy Bypass -File build.ps1 [-Configuration Release] [-Dotnet <path\to\dotnet.exe>]
#
# dist\ then holds PlayOutCoop.exe, the engine (Nucleus.Gaming.dll), Jint and the other managed dependencies,
# Nucleus.IJx86.exe / Nucleus.IJx64.exe, StartGame.exe, EasyHook.dll, LICENSE and README.md. The native Nucleus files
# (hook DLLs, ProtoInput, openxinput, utils\) are not built here: PlayOut extracts them from NucleusApp.zip.
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Dotnet = ''
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

if (-not $Dotnet) {
    # The first dotnet.exe that has an SDK installed (a runtime-only dotnet on PATH cannot build).
    $candidates = @("$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe")
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    $candidates += "$env:ProgramFiles\dotnet\dotnet.exe"
    foreach ($c in $candidates) {
        if ((Test-Path $c) -and (& $c --list-sdks 2>$null)) { $Dotnet = $c; break }
    }
    if (-not $Dotnet) { throw 'No dotnet.exe with an SDK (.NET 8 or newer) was found. Pass -Dotnet <path>.' }
}

Write-Host "Building PlayOutCoop.sln ($Configuration) with $Dotnet"
& $Dotnet build (Join-Path $repo 'PlayOutCoop.sln') -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }

$out = Join-Path $repo "bin\$Configuration"
$dist = Join-Path $repo 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

# Shipped files: every exe/dll/config the build put in bin\<Configuration>\ (test projects build elsewhere).
Get-ChildItem $out -File | Where-Object { $_.Extension -in '.exe', '.dll', '.config' } |
    Copy-Item -Destination $dist

Copy-Item (Join-Path $repo 'LICENSE') $dist
Copy-Item (Join-Path $repo 'README.md') $dist
Copy-Item (Join-Path $repo 'CONTRACT.md') $dist

$required = 'PlayOutCoop.exe', 'PlayOutCoop.exe.config', 'Nucleus.Gaming.dll', 'Jint.dll', 'WindowScrape.dll',
    'Newtonsoft.Json.dll', 'Ionic.Zip.Reduced.dll', 'SharpDX.dll', 'SharpDX.DirectInput.dll', 'SharpDX.XInput.dll',
    'EasyHook.dll', 'Nucleus.IJx86.exe', 'Nucleus.IJx64.exe', 'StartGame.exe', 'System.Resources.Extensions.dll'
$missing = $required | Where-Object { -not (Test-Path (Join-Path $dist $_)) }
if ($missing) { throw "dist is missing: $($missing -join ', ')" }

Write-Host "dist ready: $dist"
Get-ChildItem $dist -File | Sort-Object Name | ForEach-Object { '  {0,-45} {1,10:N0}' -f $_.Name, $_.Length }
