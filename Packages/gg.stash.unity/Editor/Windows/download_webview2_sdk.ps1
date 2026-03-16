# Script to download and extract WebView2 SDK

$ErrorActionPreference = "Stop"

Write-Host "WebView2 SDK Download Script" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

$sdkPath = "$PSScriptRoot\WebView2SDK"
$expectedIncludePath = "$sdkPath\build\native\include"
$expectedLibPath = "$sdkPath\build\native\x64\WebView2LoaderStatic.lib"

# Check if SDK already exists
if (Test-Path $expectedIncludePath) {
    $headerPath = Get-ChildItem -Path $expectedIncludePath -Filter "WebView2.h" -ErrorAction SilentlyContinue
    if ($headerPath -and (Test-Path $expectedLibPath)) {
        Write-Host "WebView2 SDK already downloaded!" -ForegroundColor Green
        Write-Host "SDK location: $sdkPath" -ForegroundColor Green
        Write-Host ""
        exit 0
    }
}

Write-Host "Downloading WebView2 SDK..." -ForegroundColor Yellow
Write-Host ""

# WebView2 SDK NuGet package URL (latest stable version)
$nugetPackageUrl = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2"
$tempDir = "$env:TEMP\WebView2SDK_Download"
$nupkgFile = "$tempDir\Microsoft.Web.WebView2.nupkg"

try {
    # Create temp directory
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    # Download the NuGet package
    # First, get the latest version from NuGet API
    Write-Host "Fetching latest version information..." -ForegroundColor Cyan
    $packageInfoUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/index.json"
    
    try {
        $packageInfo = Invoke-RestMethod -Uri $packageInfoUrl -UseBasicParsing -ErrorAction Stop
        $latestVersion = $packageInfo.versions | Sort-Object -Descending | Select-Object -First 1
        Write-Host "Latest version: $latestVersion" -ForegroundColor Green
        
        # Download specific version
        $downloadUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$latestVersion/microsoft.web.webview2.$latestVersion.nupkg"
    } catch {
        Write-Host "Warning: Could not fetch version info, trying direct download..." -ForegroundColor Yellow
        $downloadUrl = $nugetPackageUrl
    }
    
    Write-Host "Downloading package (this may take a moment)..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $downloadUrl -OutFile $nupkgFile -UseBasicParsing -ErrorAction Stop
    
    if (-not (Test-Path $nupkgFile)) {
        throw "Download failed - file not found"
    }
    
    Write-Host "Download complete!" -ForegroundColor Green
    Write-Host ""
    
    # Extract the nupkg (it's just a zip file)
    Write-Host "Extracting package..." -ForegroundColor Cyan
    $extractDir = "$tempDir\extracted"
    New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
    
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkgFile, $extractDir)
    
    # Find the native SDK files
    $nativePath = Get-ChildItem -Path $extractDir -Recurse -Directory -Filter "native" -ErrorAction SilentlyContinue | Select-Object -First 1
    
    if (-not $nativePath) {
        throw "Could not find 'native' directory in the extracted package"
    }
    
    # Create SDK directory structure
    if (Test-Path $sdkPath) {
        Remove-Item $sdkPath -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $sdkPath -Force | Out-Null
    
    # Copy the native SDK files
    Write-Host "Installing SDK files..." -ForegroundColor Cyan
    $sourceNativePath = $nativePath.FullName
    $destNativePath = "$sdkPath\build\native"
    
    New-Item -ItemType Directory -Path (Split-Path $destNativePath -Parent) -Force | Out-Null
    Copy-Item -Path $sourceNativePath -Destination $destNativePath -Recurse -Force
    
    # Verify installation
    if (Test-Path $expectedIncludePath) {
        $headerPath = Get-ChildItem -Path $expectedIncludePath -Filter "WebView2.h" -ErrorAction SilentlyContinue
        if ($headerPath -and (Test-Path $expectedLibPath)) {
            Write-Host ""
            Write-Host "SUCCESS: WebView2 SDK downloaded and installed!" -ForegroundColor Green
            Write-Host "SDK location: $sdkPath" -ForegroundColor Green
            Write-Host "Header path: $expectedIncludePath" -ForegroundColor Green
            Write-Host "Library path: $expectedLibPath" -ForegroundColor Green
        } else {
            throw "SDK files not found after installation"
        }
    } else {
        throw "SDK installation directory not found"
    }
    
    # Cleanup
    Write-Host ""
    Write-Host "Cleaning up temporary files..." -ForegroundColor Gray
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "Script completed successfully!" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to download WebView2 SDK" -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "You can manually download the SDK from:" -ForegroundColor Yellow
    Write-Host "https://www.nuget.org/packages/Microsoft.Web.WebView2" -ForegroundColor Cyan
    Write-Host ""
    
    # Cleanup on error
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    exit 1
}

