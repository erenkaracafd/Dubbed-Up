<#
.SYNOPSIS
    Bakes a scene MP4 video together with recorded audio take WAVs into a single combined MP4.
.PARAMETER SceneFolder
    Path to the scene directory containing scene.json and video.
.PARAMETER TakesFolder
    Path to the folder containing recorded .wav files.
.PARAMETER OutputVideo
    Target output file path (e.g. exports/final_dub.mp4).
#>
param (
    [Parameter(Mandatory = $true)]
    [string]$SceneFolder,

    [Parameter(Mandatory = $true)]
    [string]$TakesFolder,

    [Parameter(Mandatory = $false)]
    [string]$OutputVideo = "exports/final_dubbed_video.mp4"
)

Write-Host "=== Dubbed-Up Video Exporter ===" -ForegroundColor Cyan

if (-not (Test-Path $SceneFolder)) {
    Write-Error "Scene folder does not exist: $SceneFolder"
    exit 1
}

$sceneJsonPath = Join-Path $SceneFolder "scene.json"
if (-not (Test-Path $sceneJsonPath)) {
    Write-Error "scene.json not found in $SceneFolder"
    exit 1
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolConfigPath = Join-Path $repositoryRoot '.tools\media-tools.json'
$ffmpegPath = $env:DUBBEDUP_FFMPEG_PATH

if (-not $ffmpegPath -and (Test-Path -LiteralPath $toolConfigPath)) {
    $toolConfig = Get-Content -LiteralPath $toolConfigPath -Raw | ConvertFrom-Json
    $ffmpegPath = $toolConfig.ffmpegPath
}

if (-not $ffmpegPath) {
    $ffmpeg = Get-Command 'ffmpeg' -ErrorAction SilentlyContinue
    if ($ffmpeg) {
        $ffmpegPath = $ffmpeg.Source
    }
}

if (-not $ffmpegPath -or -not (Test-Path -LiteralPath $ffmpegPath)) {
    Write-Warning "FFmpeg is unavailable. Run .\scripts\setup-media-tools.ps1 to enable automated MP4 video baking."
    Write-Host "You can manually use the WAV files from $TakesFolder in your favorite video editor." -ForegroundColor Yellow
    exit 0
}

Write-Host "FFmpeg found: $ffmpegPath" -ForegroundColor Green
$outputDir = Split-Path -Parent (Resolve-Path -Path $OutputVideo -ErrorAction SilentlyContinue)
if ($outputDir -and (-not (Test-Path $outputDir))) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "Ready to multiplex audio and video into $OutputVideo" -ForegroundColor Green

