<#
.SYNOPSIS
Uninstalls PrintPilotProxy

.DESCRIPTION
Stops and removes the Windows Service, deletes the application binaries from Program Files,
removes the Start Menu shortcut, and cleans up the firewall rule.

User configuration (ProgramData) is PRESERVED.
#>

# Requires Run as Administrator
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Please run this script as Administrator."
    Pause
    Exit
}

$InstallDir = "C:\Program Files\PrintPilotProxy"
$ServiceName = "PrintPilotProxy"

Write-Host "Uninstalling PrintPilotProxy..." -ForegroundColor Cyan

# 1. Stop and remove the service
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping and removing PrintPilotProxy service..."
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName
}

# 2. Clean up Firewall rules
Write-Host "Cleaning up Windows Firewall rules..."
# Remove the rules by name
netsh advfirewall firewall delete rule name="PrintPilotProxy" 2>$null
netsh advfirewall firewall delete rule name="PrintPilotProxy Discovery (UDP-In)" 2>$null

# 3. Remove Start Menu Shortcut
Write-Host "Removing Start Menu shortcut..."
$StartMenuPath = [Environment]::GetFolderPath('CommonStartMenu')
$ShortcutPath = Join-Path $StartMenuPath "Programs\PrintPilotProxy.lnk"
if (Test-Path $ShortcutPath) {
    Remove-Item -Path $ShortcutPath -Force
}

# 4. Remove Binaries
Write-Host "Removing binaries from $InstallDir..."
if (Test-Path $InstallDir) {
    Remove-Item -Path $InstallDir -Recurse -Force
}

Write-Host ""
Write-Host "PrintPilotProxy uninstalled successfully!" -ForegroundColor Green
Write-Host "Note: Your configuration and logs in C:\ProgramData\PrintPilotProxy were preserved." -ForegroundColor Yellow
Write-Host ""
Pause
