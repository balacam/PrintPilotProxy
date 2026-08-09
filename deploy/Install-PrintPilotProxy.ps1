<#
.SYNOPSIS
Installs PrintPilotProxy v0.1.0

.DESCRIPTION
This script copies the compiled binaries to Program Files, creates the ProgramData configuration directory,
installs the background Windows Service, and creates a Start Menu shortcut for the management application.

.NOTES
Requires Administrator Privileges.
#>

# Requires Run as Administrator
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Please run this script as Administrator."
    Pause
    Exit
}

$InstallDir = "C:\Program Files\PrintPilotProxy"
$DataDir = "C:\ProgramData\PrintPilotProxy"
$ServiceName = "PrintPilotProxy"

Write-Host "Installing PrintPilotProxy..." -ForegroundColor Cyan

# 1. Stop existing service if upgrading
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping existing PrintPilotProxy service..."
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 2
}

# 2. Create directories
Write-Host "Creating directories..."
if (!(Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }
if (!(Test-Path $DataDir)) { New-Item -ItemType Directory -Path $DataDir -Force | Out-Null }

# 3. Copy binaries (Assumes this script is run from the extracted release ZIP)
$SourcePath = Join-Path $PSScriptRoot "bin"
if (Test-Path $SourcePath) {
    Write-Host "Copying binaries to $InstallDir..."
    Copy-Item -Path "$SourcePath\*" -Destination $InstallDir -Recurse -Force
} else {
    Write-Warning "Could not find 'bin' directory next to installer script. Make sure you extracted the ZIP completely."
}

# 4. Install Windows Service
Write-Host "Configuring Windows Service..."
$ServiceExe = Join-Path $InstallDir "PrintPilotProxy.Service.exe"
if (Test-Path $ServiceExe) {
    if (!$service) {
        # Create service
        sc.exe create $ServiceName binPath= "`"$ServiceExe`"" DisplayName= "PrintPilotProxy Forward Proxy Service" start= auto
        
        # Configure recovery (restart on failure)
        sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000
    }
    
    # Start the service
    Write-Host "Starting PrintPilotProxy service..."
    Start-Service -Name $ServiceName
}

# 5. Create Start Menu Shortcut
Write-Host "Creating Start Menu shortcut..."
$StartMenuPath = [Environment]::GetFolderPath('CommonStartMenu')
$ShortcutPath = Join-Path $StartMenuPath "Programs\PrintPilotProxy.lnk"
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = Join-Path $InstallDir "PrintPilotProxy.App.exe"
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "PrintPilotProxy Management Application"
$Shortcut.Save()

Write-Host ""
Write-Host "PrintPilotProxy installed successfully!" -ForegroundColor Green
Write-Host "You can now open 'PrintPilotProxy' from the Start Menu to configure allowed clients." -ForegroundColor Yellow
Write-Host ""
Pause
