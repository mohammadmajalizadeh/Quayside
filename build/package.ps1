# Builds the portable zip and (if Inno Setup is installed) the installer into .\artifacts
param(
    [string]$Version = "0.1.0",
    [string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"

Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $root "src/Quayside.App/Quayside.App.csproj") -c Release -r $Runtime `
    --self-contained true -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$zip = Join-Path $artifacts "Quayside-$Version-$Runtime-portable.zip"
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip
Write-Host "Portable zip: $zip"

$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) { $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" }
if (Test-Path $iscc) {
    & $iscc "/DAppVersion=$Version" "/DSourceDir=$publish" (Join-Path $root "installer/Quayside.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
} else {
    Write-Warning "Inno Setup (ISCC.exe) not found; installer skipped. Install with: choco install innosetup"
}
