param (
    [string]$Version
)
$ErrorActionPreference = "Stop"

$rootDir = $PSScriptRoot | Split-Path -Parent
Set-Location $rootDir

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$props = Get-Content "Directory.Build.props"
    $Version = $props.Project.PropertyGroup.Version.Trim()
}

Write-Host "==> Packaging version: $Version" -ForegroundColor Cyan

Write-Host "==> Stopping running instances if any..." -ForegroundColor Cyan
Get-Process PrintPilotProxy.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process PrintPilotProxy.Cli -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Host "==> Cleaning publish directory..." -ForegroundColor Cyan
if (Test-Path "publish") { Get-ChildItem "publish" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
New-Item "publish/staging/App" -ItemType Directory -Force | Out-Null
New-Item "publish/staging/Service" -ItemType Directory -Force | Out-Null
New-Item "publish/staging/Cli" -ItemType Directory -Force | Out-Null

Write-Host "==> Publishing self-contained binaries (win-x64 Release)..." -ForegroundColor Cyan
dotnet publish src/PrintPilotProxy.App/PrintPilotProxy.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o publish/staging/App
dotnet publish src/PrintPilotProxy.Service/PrintPilotProxy.Service.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o publish/staging/Service
dotnet publish src/PrintPilotProxy.Cli/PrintPilotProxy.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:Version=$Version -o publish/staging/Cli

Write-Host "==> Building WiX MSI Installer..." -ForegroundColor Cyan
dotnet build src/PrintPilotProxy.Installer/PrintPilotProxy.Installer.wixproj -c Release -p:OutputName="PrintPilotProxy-$Version-x64"

Write-Host "==> Creating Portable ZIP archive..." -ForegroundColor Cyan
$portableStage = "publish/PrintPilotProxy-$Version-win-x64"
New-Item "$portableStage/bin/App" -ItemType Directory -Force | Out-Null
New-Item "$portableStage/bin/Service" -ItemType Directory -Force | Out-Null
New-Item "$portableStage/bin/Cli" -ItemType Directory -Force | Out-Null

Copy-Item publish/staging/App/* "$portableStage/bin/App" -Recurse -Force
Copy-Item publish/staging/Service/* "$portableStage/bin/Service" -Recurse -Force
Copy-Item publish/staging/Cli/* "$portableStage/bin/Cli" -Recurse -Force
Copy-Item README.md "$portableStage" -Force
Copy-Item THIRD-PARTY-NOTICES.md "$portableStage" -Force
if (Test-Path "LICENSE") { Copy-Item LICENSE "$portableStage" -Force }

$portableZip = "publish/PrintPilotProxy-$Version-win-x64.zip"
Compress-Archive -Path "$portableStage/*" -DestinationPath $portableZip -Force
Remove-Item $portableStage -Recurse -Force

Write-Host "==> Calculating SHA256 hashes..." -ForegroundColor Cyan
$shaFile = "publish/SHA256SUMS.txt"
$hashes = @()
$msiFile = "publish/PrintPilotProxy-$Version-x64.msi"

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
Get-ChildItem publish | Select-Object Name, Length
