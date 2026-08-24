# Dubbed-Up Launcher Script
$godotExe = "C:\Users\SÜLEYMAN\Desktop\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
$projectPath = Join-Path $PSScriptRoot "src\DubbedUp.Godot"

if (-not (Test-Path $godotExe)) {
    # Fallback to searching in PATH
    $godotCommand = Get-Command "godot" -ErrorAction SilentlyContinue
    if ($godotCommand) {
        $godotExe = $godotCommand.Source
    }
}

if (-not (Test-Path $godotExe)) {
    Write-Error "Godot executable not found at: $godotExe"
    exit 1
}

Write-Host "Starting Dubbed-Up with Godot Mono..." -ForegroundColor Green
Start-Process -FilePath $godotExe -ArgumentList "--path `"$projectPath`""
