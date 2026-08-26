<#
.SYNOPSIS
    Removes DELIMa machine-wide enterprise policies for Google Chrome and Microsoft Edge.

.DESCRIPTION
    When DELIMa Kiosk Hardening / Browser Policies are applied to HKLM, Chrome and Edge
    enforce URLBlocklist="*" which blocks all websites other than DELIMa portals for
    every user on the computer.

    Running this script with Administrator privileges removes the HKLM policy keys and
    instantly restores normal browsing on Google Chrome and Microsoft Edge.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Remove-BrowserPolicies.ps1
#>

# Ensure script is run with Administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "Sila jalankan PowerShell ini sebagai Pentadbir (Run as Administrator)."
    Exit 1
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  DELIMa - Nyahkan Sekatan Pelayar (Chrome & Microsoft Edge) " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Google Chrome Policies
$chromeKey = "HKLM:\SOFTWARE\Policies\Google\Chrome"
if (Test-Path $chromeKey) {
    Write-Host "[1/2] Memadamkan dasar sekatan Google Chrome..." -ForegroundColor Yellow
    Remove-Item -Path "$chromeKey\URLBlocklist" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$chromeKey\URLAllowlist" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $chromeKey -Name "PasswordManagerEnabled" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $chromeKey -Name "DeveloperToolsAvailability" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $chromeKey -Name "IncognitoModeAvailability" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $chromeKey -Name "BrowserSignin" -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Dasar Google Chrome berjaya dipadamkan." -ForegroundColor Green
} else {
    Write-Host "[1/2] Tiada dasar sekatan Google Chrome ditemui." -ForegroundColor Gray
}

# 2. Microsoft Edge Policies
$edgeKey = "HKLM:\SOFTWARE\Policies\Microsoft\Edge"
if (Test-Path $edgeKey) {
    Write-Host "[2/2] Memadamkan dasar sekatan Microsoft Edge..." -ForegroundColor Yellow
    Remove-Item -Path "$edgeKey\URLBlocklist" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$edgeKey\URLAllowlist" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $edgeKey -Name "PasswordManagerEnabled" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $edgeKey -Name "DeveloperToolsAvailability" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $edgeKey -Name "InPrivateModeAvailability" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $edgeKey -Name "BrowserSignin" -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Dasar Microsoft Edge berjaya dipadamkan." -ForegroundColor Green
} else {
    Write-Host "[2/2] Tiada dasar sekatan Microsoft Edge ditemui." -ForegroundColor Gray
}

Write-Host "`n[SELESAI] Sekatan pelayar telah dibatalkan." -ForegroundColor Green
Write-Host "Sila tutup dan buka semula Google Chrome / Edge untuk melayari internet seperti biasa.`n" -ForegroundColor White
