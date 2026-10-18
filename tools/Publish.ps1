# Genera los entregables en .\artifacts:
#   WakeOnLan-<versión>-portable.exe  -> un único .exe, no requiere instalar .NET
#   WakeOnLan-<versión>-setup.exe     -> instalador (si Inno Setup 6 está instalado)
#   SHA256SUMS.txt                    -> hashes para verificar las descargas
#
# Uso: powershell -ExecutionPolicy Bypass -File tools\Publish.ps1 [-Version 1.0.0]
param(
    # Versión semántica estricta: el valor acaba en argumentos de dotnet e Inno Setup.
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$project = Join-Path $root 'WakeOnLan.csproj'
$artifacts = Join-Path $root 'artifacts'

Remove-Item -Recurse -Force $artifacts -ErrorAction SilentlyContinue

$common = @('-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:DebugType=none', "-p:Version=$Version")

# Portable: un solo archivo. Las librerías nativas de WPF se extraen a %TEMP% al arrancar.
dotnet publish $project @common `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o (Join-Path $artifacts 'publish-portable')
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (portable) falló.' }

Copy-Item (Join-Path $artifacts 'publish-portable\WakeOnLan.exe') (Join-Path $artifacts "WakeOnLan-$Version-portable.exe")

# Instalador: carpeta normal, así todas las DLL se cargan desde la carpeta de instalación y no desde %TEMP%.
dotnet publish $project @common -o (Join-Path $artifacts 'publish')
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (instalador) falló.' }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc /Q "/DAppVersion=$Version" (Join-Path $root 'installer\WakeOnLan.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup falló.' }
} else {
    Write-Warning 'Inno Setup 6 no está instalado: se omite el instalador (winget install JRSoftware.InnoSetup).'
}

# Formato compatible con `sha256sum -c`.
Get-ChildItem $artifacts -Filter *.exe -File |
    ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding ascii

Get-ChildItem $artifacts -File | Select-Object Name, @{ n = 'MB'; e = { [Math]::Round($_.Length / 1MB, 1) } }
