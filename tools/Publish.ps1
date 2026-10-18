# Genera los entregables en .\artifacts:
#   WakeOnLan-<versión>-portable.exe  -> un único .exe, no requiere instalar .NET
#   WakeOnLan-<versión>-setup.exe     -> instalador (si Inno Setup 6 está instalado)
#
# Uso: powershell -ExecutionPolicy Bypass -File tools\Publish.ps1 [-Version 1.0.0]
param([string]$Version = '1.0.0')

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'

Remove-Item -Recurse -Force $artifacts -ErrorAction SilentlyContinue

dotnet publish (Join-Path $root 'WakeOnLan.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falló.' }

Copy-Item (Join-Path $publishDir 'WakeOnLan.exe') (Join-Path $artifacts "WakeOnLan-$Version-portable.exe")

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc "/DAppVersion=$Version" (Join-Path $root 'installer\WakeOnLan.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup falló.' }
} else {
    Write-Warning 'Inno Setup 6 no está instalado: se omite el instalador (winget install JRSoftware.InnoSetup).'
}

Get-ChildItem $artifacts -File | Select-Object Name, @{ n = 'MB'; e = { [Math]::Round($_.Length / 1MB, 1) } }
