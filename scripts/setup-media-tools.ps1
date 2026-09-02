[CmdletBinding()]
param(
    [switch]$SkipFfmpeg,
    [switch]$SkipVisualCpp,
    [switch]$SkipModelDownload
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $repositoryRoot '.tools'
$whisperRoot = Join-Path $toolsRoot 'whisper'
$whisperPython = Join-Path $whisperRoot 'Scripts\python.exe'
$requirements = Join-Path $PSScriptRoot 'requirements-whisper.txt'
$configPath = Join-Path $toolsRoot 'media-tools.json'

function Find-Ffmpeg {
    $command = Get-Command 'ffmpeg' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $wingetPackages = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    if (Test-Path -LiteralPath $wingetPackages) {
        $candidate = Get-ChildItem -LiteralPath $wingetPackages -Directory -Filter 'Gyan.FFmpeg*' -ErrorAction SilentlyContinue |
            ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter 'ffmpeg.exe' -File -ErrorAction SilentlyContinue } |
            Sort-Object -Property FullName -Descending |
            Select-Object -First 1
        if ($candidate) {
            return $candidate.FullName
        }
    }

    return $null
}

if (-not $SkipFfmpeg) {
    $ffmpegPath = Find-Ffmpeg
    if (-not $ffmpegPath) {
        $winget = Get-Command 'winget' -ErrorAction SilentlyContinue
        if (-not $winget) {
            throw 'FFmpeg is missing and WinGet is unavailable. Install Gyan.FFmpeg, then rerun this script.'
        }

        & $winget.Source install --id Gyan.FFmpeg --exact --accept-package-agreements --accept-source-agreements --silent --disable-interactivity
        if ($LASTEXITCODE -ne 0) {
            throw "WinGet failed to install FFmpeg (exit code $LASTEXITCODE)."
        }
        $ffmpegPath = Find-Ffmpeg
    }
} else {
    $ffmpegPath = Find-Ffmpeg
}

if (-not $ffmpegPath) {
    throw 'FFmpeg could not be located after setup.'
}

if (-not $SkipVisualCpp) {
    $winget = Get-Command 'winget' -ErrorAction SilentlyContinue
    if (-not $winget) {
        throw 'WinGet is required to install the Microsoft Visual C++ runtime used by PyTorch.'
    }

    & $winget.Source install --id 'Microsoft.VCRedist.2015+.x64' --exact --accept-package-agreements --accept-source-agreements --silent --disable-interactivity
    if ($LASTEXITCODE -notin @(0, -1978335189)) {
        throw "WinGet failed to install the Microsoft Visual C++ runtime (exit code $LASTEXITCODE)."
    }
}

$python = Get-Command 'python' -ErrorAction SilentlyContinue
if (-not $python) {
    throw 'Python 3.10 or newer is required to install local Whisper.'
}

New-Item -ItemType Directory -Path $toolsRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $whisperPython)) {
    & $python.Source -m venv $whisperRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Python failed to create the Whisper virtual environment (exit code $LASTEXITCODE)."
    }
}

& $whisperPython -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) {
    throw "pip upgrade failed (exit code $LASTEXITCODE)."
}

& $whisperPython -m pip install --requirement $requirements
if ($LASTEXITCODE -ne 0) {
    throw "Whisper installation failed (exit code $LASTEXITCODE)."
}

$ffmpegDirectory = Split-Path -Parent $ffmpegPath
$env:Path = "$ffmpegDirectory;$env:Path"

try {
    & $whisperPython -c "import whisper, torch; print(f'Whisper ready; PyTorch {torch.__version__}')"
    if ($LASTEXITCODE -ne 0) {
        throw 'Whisper import verification failed.'
    }
} catch {
    throw "Whisper could not load. Install Microsoft Visual C++ 2015-2022 Redistributable (x64), then rerun this script. $($_.Exception.Message)"
}

if (-not $SkipModelDownload) {
    & $whisperPython -c "import whisper; whisper.load_model('tiny'); print('Whisper tiny model ready')"
    if ($LASTEXITCODE -ne 0) {
        throw "Whisper tiny model download failed (exit code $LASTEXITCODE)."
    }
}

$config = [ordered]@{
    schemaVersion = 1
    ffmpegPath = [System.IO.Path]::GetFullPath($ffmpegPath)
    whisperPythonPath = [System.IO.Path]::GetFullPath($whisperPython)
    whisperModel = 'tiny'
}
$config | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding utf8

& $ffmpegPath -version | Select-Object -First 1
Write-Host "Media tools are ready. Local config: $configPath" -ForegroundColor Green
