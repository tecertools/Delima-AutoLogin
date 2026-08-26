<#
.SYNOPSIS
    Builds the self-contained executables and packages the Inno Setup Windows installer for DELIMa Smart Launcher.

.DESCRIPTION
    1. Reads version from Directory.Build.props (or accepts -Version override).
    2. Publishes Delima.Launcher, Delima.Admin, and Delima.Provision as win-x64 single-file executables.
    3. Invokes Inno Setup (ISCC.exe) to generate the installer inside .\dist\.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Version "2.2.1"
#>

[CmdletBinding()]
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1. Resolve Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $propsPath = Join-Path $ScriptDir "Directory.Build.props"
    if (Test-Path $propsPath) {
        [xml]$propsXml = Get-Content $propsPath
        $Version = $propsXml.Project.PropertyGroup.Version
    }
    if ([string]::IsNullOrWhiteSpace($Version)) {
        $Version = "2.2.1"
    }
}

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " Building DELIMa Smart Launcher Installer v$Version " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# 2. Locate Inno Setup Compiler (ISCC.exe)
$candidatePaths = @(
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe"
)
$foundIscc = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $foundIscc) {
    $cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        $foundIscc = $cmd.Source
    }
}

if (-not $foundIscc) {
    Write-Error "Inno Setup 6 (ISCC.exe) not found. Please install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}
$iscc = $foundIscc
Write-Host "Using Inno Setup: $iscc" -ForegroundColor Gray

# 3. Publish Executables
$publishArgs = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "/p:PublishSingleFile=true"
)

$projects = @(
    @{ Name = "Launcher";  Path = "src\Delima.Launcher\Delima.Launcher.csproj";   Out = "publish\Launcher" },
    @{ Name = "Admin";     Path = "src\Delima.Admin\Delima.Admin.csproj";         Out = "publish\Admin" },
    @{ Name = "Provision"; Path = "src\Delima.Provision\Delima.Provision.csproj"; Out = "publish\Provision" }
)

foreach ($proj in $projects) {
    Write-Host "`n--> Publishing $($proj.Name)..." -ForegroundColor Yellow
    $fullProjPath = Join-Path $ScriptDir $proj.Path
    $fullOutPath  = Join-Path $ScriptDir $proj.Out
    
    & dotnet publish $fullProjPath @publishArgs -o $fullOutPath
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish $($proj.Name)."
    }
}

# 4. Compile Inno Setup Installer
Write-Host "`n--> Compiling Inno Setup Installer..." -ForegroundColor Yellow
$issPath = Join-Path $ScriptDir "installer\DelimaLauncher.iss"
& $iscc "/DMyAppVersion=$Version" $issPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed."
}

# 5. Output Summary
$outputExe = Join-Path $ScriptDir "dist\DELIMaLauncher-Setup-$Version.exe"
if (Test-Path $outputExe) {
    $item = Get-Item $outputExe
    $hash = (Get-FileHash $outputExe -Algorithm SHA256).Hash
    $sizeMb = [Math]::Round($item.Length / 1MB, 2)
    
    Write-Host "`n====================================================" -ForegroundColor Green
    Write-Host " Installer created successfully!" -ForegroundColor Green
    Write-Host " File:   $($item.FullName)" -ForegroundColor White
    Write-Host " Size:   $sizeMb MB" -ForegroundColor White
    Write-Host " SHA256: $hash" -ForegroundColor White
    Write-Host "====================================================" -ForegroundColor Green
}
