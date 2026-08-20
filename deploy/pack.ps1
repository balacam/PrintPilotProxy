param (
    [string]$Version
)
$ErrorActionPreference = "Stop"

$rootDir = $PSScriptRoot | Split-Path -Parent
Set-Location $rootDir
$publishDir = Join-Path $rootDir "publish"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content (Join-Path $rootDir "Directory.Build.props")
    $Version = $props.Project.PropertyGroup.Version.Trim()
}

Write-Host "==> Packaging version: $Version" -ForegroundColor Cyan

Write-Host "==> Stopping running instances if any..." -ForegroundColor Cyan
Get-Process PrintPilotProxy.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process PrintPilotProxy.Cli -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "==> Cleaning staging directory..." -ForegroundColor Cyan
if (Test-Path "$publishDir\staging") { Remove-Item "$publishDir\staging" -Recurse -Force -ErrorAction SilentlyContinue }
New-Item "$publishDir\staging\App" -ItemType Directory -Force | Out-Null
New-Item "$publishDir\staging\Service" -ItemType Directory -Force | Out-Null
New-Item "$publishDir\staging\Cli" -ItemType Directory -Force | Out-Null

Write-Host "==> Publishing self-contained binaries (win-x64 Release)..." -ForegroundColor Cyan
dotnet publish src/PrintPilotProxy.App/PrintPilotProxy.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o "$publishDir\staging\App"
dotnet publish src/PrintPilotProxy.Service/PrintPilotProxy.Service.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o "$publishDir\staging\Service"
dotnet publish src/PrintPilotProxy.Cli/PrintPilotProxy.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o "$publishDir\staging\Cli"

Write-Host "==> Building WiX MSI Installer..." -ForegroundColor Cyan
dotnet build src/PrintPilotProxy.Installer/PrintPilotProxy.Installer.wixproj -c Release -p:OutputName="PrintPilotProxy-$Version-x64"

Write-Host "==> Creating Portable ZIP archive..." -ForegroundColor Cyan
$portableStage = Join-Path $rootDir "obj_portable_stage"
if (Test-Path $portableStage) { Remove-Item $portableStage -Recurse -Force }
New-Item "$portableStage\bin\App" -ItemType Directory -Force | Out-Null
New-Item "$portableStage\bin\Service" -ItemType Directory -Force | Out-Null
New-Item "$portableStage\bin\Cli" -ItemType Directory -Force | Out-Null

Copy-Item "$publishDir\staging\App\*" "$portableStage\bin\App" -Recurse -Force
Copy-Item "$publishDir\staging\Service\*" "$portableStage\bin\Service" -Recurse -Force
Copy-Item "$publishDir\staging\Cli\*" "$portableStage\bin\Cli" -Recurse -Force
Copy-Item (Join-Path $rootDir "README.md") "$portableStage" -Force
Copy-Item (Join-Path $rootDir "THIRD-PARTY-NOTICES.md") "$portableStage" -Force
if (Test-Path (Join-Path $rootDir "LICENSE")) { Copy-Item (Join-Path $rootDir "LICENSE") "$portableStage" -Force }

$portableZip = Join-Path $publishDir "PrintPilotProxy-$Version-win-x64.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Compress-Archive -Path "$portableStage\*" -DestinationPath $portableZip -Force
Remove-Item $portableStage -Recurse -Force

Write-Host "==> Calculating SHA256 hashes..." -ForegroundColor Cyan
$shaFile = Join-Path $publishDir "SHA256SUMS.txt"
$hashes = @()
$msiFile = Join-Path $publishDir "PrintPilotProxy-$Version-x64.msi"

if (Test-Path $msiFile) {
    $msiHash = (Get-FileHash $msiFile -Algorithm SHA256).Hash.ToLower()
    $hashes += "$msiHash  PrintPilotProxy-$Version-x64.msi"
}

if (Test-Path $portableZip) {
    $zipHash = (Get-FileHash $portableZip -Algorithm SHA256).Hash.ToLower()
    $hashes += "$zipHash  PrintPilotProxy-$Version-win-x64.zip"
}

$hashes | Out-File -FilePath $shaFile -Encoding utf8

Write-Host "==> RELEASE PACKAGING COMPLETE!" -ForegroundColor Green
Get-ChildItem $publishDir | Select-Object Name, Length
