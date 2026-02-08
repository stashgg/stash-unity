# Simple build script that builds directly to Unity Assets folder
# Run this from PowerShell (or Visual Studio Developer PowerShell)

$ErrorActionPreference = "Stop"

# Function to pause and show error before exiting
function Exit-WithError {
    param([string]$Message)
    Write-Host ""
    Write-Host "ERROR: $Message" -ForegroundColor Red
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

# Trap all errors
trap {
    Write-Host ""
    Write-Host "UNHANDLED ERROR: $_" -ForegroundColor Red
    Write-Host "Error at line: $($_.InvocationInfo.ScriptLineNumber)" -ForegroundColor Red
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

try {
    $winDir = $PSScriptRoot
    if (-not $winDir -or -not (Test-Path $winDir)) {
        Exit-WithError "Could not determine script directory"
    }
    $cppFile = Join-Path $winDir "WebViewLauncher.cpp"
    $dllFile = Join-Path $winDir "WebViewLauncher.dll"
    $webview2Include = Join-Path $winDir "WebView2SDK\build\native\include"
    $webview2Lib = Join-Path $winDir "WebView2SDK\build\native\x64\WebView2LoaderStatic.lib"
    
    Write-Host "Script directory: $winDir" -ForegroundColor Gray
} catch {
    Exit-WithError "Failed to set up paths: $_"
}

# Check if C++ source file exists
if (-not (Test-Path $cppFile)) {
    Exit-WithError "C++ source file not found: $cppFile"
}

# Check if Unity is running (more accurate check)
Write-Host "Checking for running Unity processes..." -ForegroundColor Cyan
try {
    $unityProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object { 
        $_.ProcessName -like "*Unity*" -or 
        $_.ProcessName -like "*UnityEditor*" -or
        $_.MainWindowTitle -like "*Unity*"
    } | Where-Object { $_.Path -like "*Unity*" }
} catch {
    Write-Host "  Warning: Could not check for Unity processes: $_" -ForegroundColor Yellow
    $unityProcesses = $null
}

# Also check if DLL is locked by trying to open it
$dllLocked = $false
if (Test-Path $dllFile) {
    try {
        $fileStream = [System.IO.File]::Open($dllFile, 'Open', 'ReadWrite', 'None')
        $fileStream.Close()
    } catch {
        $dllLocked = $true
        Write-Host "WARNING: DLL file is locked and cannot be overwritten!" -ForegroundColor Red
        Write-Host "This usually means Unity or another process is using the DLL." -ForegroundColor Yellow
    }
}

if ($unityProcesses -or $dllLocked) {
    if ($unityProcesses) {
        Write-Host "WARNING: Unity processes detected:" -ForegroundColor Yellow
        $unityProcesses | ForEach-Object { 
            Write-Host "  - $($_.ProcessName) (PID: $($_.Id), Path: $($_.Path))" -ForegroundColor Yellow 
        }
    }
    Write-Host ""
    Write-Host "Please close Unity completely before building the DLL." -ForegroundColor Yellow
    Write-Host "The DLL must be unlocked for the build to succeed." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Continue anyway? (y/N)"
    if ($response -ne "y" -and $response -ne "Y") {
        Write-Host ""
        Write-Host "Build cancelled by user." -ForegroundColor Yellow
        Write-Host "Press any key to exit..."
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        exit 1
    }
}

# Check for WebView2 SDK
if (-not (Test-Path $webview2Include)) {
    Exit-WithError "WebView2 SDK not found! Please run: .\download_webview2_sdk.ps1"
}

# Find Windows SDK and MSVC
Write-Host "Locating Windows SDK and MSVC..." -ForegroundColor Cyan
try {
    $winsdkPath = "C:\Program Files (x86)\Windows Kits\10\Include"
    if (-not (Test-Path $winsdkPath)) {
        Exit-WithError "Windows SDK Include directory not found: $winsdkPath"
    }
    $winsdkDirs = Get-ChildItem $winsdkPath -Directory -ErrorAction Stop
    if ($winsdkDirs.Count -eq 0) {
        Exit-WithError "No Windows SDK versions found in: $winsdkPath"
    }
    $winsdk = ($winsdkDirs | Sort-Object Name -Descending | Select-Object -First 1).Name
    Write-Host "  Found Windows SDK: $winsdk" -ForegroundColor Green
} catch {
    Exit-WithError "Failed to locate Windows SDK: $_"
}

try {
    $msvcPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC"
    if (-not (Test-Path $msvcPath)) {
        Exit-WithError "MSVC Tools directory not found: $msvcPath"
    }
    $msvcDirs = Get-ChildItem $msvcPath -Directory -ErrorAction Stop
    if ($msvcDirs.Count -eq 0) {
        Exit-WithError "No MSVC versions found in: $msvcPath"
    }
    $msvc = ($msvcDirs | Sort-Object Name -Descending | Select-Object -First 1).Name
    Write-Host "  Found MSVC: $msvc" -ForegroundColor Green
} catch {
    Exit-WithError "Failed to locate MSVC: $_"
}

if (-not $winsdk -or -not $msvc) {
    Exit-WithError "Could not find Windows SDK or MSVC. Please install Visual Studio 2022 with C++ development tools"
}

Write-Host "Building WebViewLauncher.dll..." -ForegroundColor Cyan
Write-Host "Output: $dllFile" -ForegroundColor Cyan
Write-Host ""

# Setup MSVC environment
$vcvars = "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $vcvars)) {
    Exit-WithError "vcvars64.bat not found at: $vcvars"
}

# Build using a temporary batch file
Write-Host "Preparing build files..." -ForegroundColor Cyan
$tempBat = "$env:TEMP\build_webview_unity.bat"
$rspFile = "$env:TEMP\build_webview_unity.rsp"

try {
    @"
/LD
/EHsc
/std:c++17
/I"$webview2Include"
/I"C:\Program Files (x86)\Windows Kits\10\Include\$winsdk\um"
/I"C:\Program Files (x86)\Windows Kits\10\Include\$winsdk\shared"
/I"C:\Program Files (x86)\Windows Kits\10\Include\$winsdk\winrt"
/I"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\$msvc\include"
/D"WEBVIEWLAUNCHER_EXPORTS"
/D"UNICODE"
/D"_UNICODE"
/link
/OUT:"$dllFile"
/LIBPATH:"C:\Program Files (x86)\Windows Kits\10\Lib\$winsdk\um\x64"
/LIBPATH:"C:\Program Files (x86)\Windows Kits\10\Lib\$winsdk\ucrt\x64"
"$cppFile"
"$webview2Lib"
ole32.lib
oleaut32.lib
user32.lib
gdi32.lib
winspool.lib
comdlg32.lib
advapi32.lib
shell32.lib
uuid.lib
"@ | Out-File -FilePath $rspFile -Encoding ASCII -ErrorAction Stop

    @"
@echo off
call "$vcvars"
if errorlevel 1 (
    echo ERROR: Failed to initialize MSVC environment
    exit /b 1
)
cd /d "$winDir"
if errorlevel 1 (
    echo ERROR: Failed to change to directory: $winDir
    exit /b 1
)
cl.exe @$rspFile 2>&1 | findstr /V "D9002 D9025"
if errorlevel 1 (
    echo.
    echo ========================================
    echo Build failed!
    echo ========================================
    exit /b 1
)
echo.
echo ========================================
echo Build successful!
echo DLL location: $dllFile
echo ========================================
"@ | Out-File -FilePath $tempBat -Encoding ASCII -ErrorAction Stop
} catch {
    Exit-WithError "Failed to create build files: $_"
}

Write-Host "Executing build..." -ForegroundColor Cyan
Write-Host ""

try {
    & $tempBat
    $buildExitCode = $LASTEXITCODE
} catch {
    Exit-WithError "Failed to execute build script: $_"
}

if ($buildExitCode -eq 0) {
    Write-Host ""
    Write-Host "SUCCESS: WebViewLauncher.dll built and placed in Unity Assets folder!" -ForegroundColor Green
    Write-Host "Location: $dllFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "The DLL is now part of your Unity project." -ForegroundColor Cyan
    Write-Host "If Unity was running, you may need to restart it." -ForegroundColor Cyan
    
    # Cleanup temporary files after successful build
    Write-Host ""
    Write-Host "Cleaning up temporary build files..." -ForegroundColor Gray
    
    # Remove temporary batch and response files
    $tempFilesRemoved = 0
    if (Test-Path $tempBat) {
        Remove-Item $tempBat -ErrorAction SilentlyContinue
        $tempFilesRemoved++
        Write-Host "  Removed: $tempBat" -ForegroundColor Gray
    }
    if (Test-Path $rspFile) {
        Remove-Item $rspFile -ErrorAction SilentlyContinue
        $tempFilesRemoved++
        Write-Host "  Removed: $rspFile" -ForegroundColor Gray
    }
    
    # Remove compiler-generated temporary files from the Windows directory
    $tempExtensions = @("*.obj", "*.exp", "*.pdb", "*.ilk", "*.log")
    foreach ($ext in $tempExtensions) {
        $tempFiles = Get-ChildItem -Path $winDir -Filter $ext -ErrorAction SilentlyContinue
        foreach ($file in $tempFiles) {
            try {
                Remove-Item $file.FullName -ErrorAction SilentlyContinue
                $tempFilesRemoved++
                Write-Host "  Removed: $($file.Name)" -ForegroundColor Gray
            } catch {
                # Ignore errors (file might be locked or already deleted)
            }
        }
    }
    
    # Also check for any .lib files that might have been created (except the WebView2 one we need)
    $tempLibFiles = Get-ChildItem -Path $winDir -Filter "*.lib" -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -ne "WebView2LoaderStatic.lib" -and 
        $_.Name -notlike "*WebView2*"
    }
    foreach ($file in $tempLibFiles) {
        try {
            Remove-Item $file.FullName -ErrorAction SilentlyContinue
            $tempFilesRemoved++
            Write-Host "  Removed: $($file.Name)" -ForegroundColor Gray
        } catch {
            # Ignore errors
        }
    }
    
    if ($tempFilesRemoved -gt 0) {
        Write-Host "  Cleaned up $tempFilesRemoved temporary file(s)" -ForegroundColor Green
    } else {
        Write-Host "  No temporary files found to clean up" -ForegroundColor Gray
    }
} else {
    Write-Host ""
    Write-Host "Build failed with exit code: $buildExitCode" -ForegroundColor Red
    Write-Host "Check the errors above for details." -ForegroundColor Red
    Write-Host ""
    Write-Host "Cleaning up temporary build files..." -ForegroundColor Gray
    
    # Still try to clean up even on failure
    Remove-Item $tempBat -ErrorAction SilentlyContinue
    Remove-Item $rspFile -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host ""
Write-Host "Script completed successfully!" -ForegroundColor Green
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

