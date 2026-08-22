$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$requiredPaths = @(
    'AGENTS.md',
    'README.md',
    'THIRD_PARTY_NOTICES.md',
    'docs/ARCHITECTURE.md',
    'docs/CONTENT_POLICY.md',
    'docs/DEVELOPMENT_WORKFLOW.md',
    'docs/MVP_SCOPE.md',
    'docs/PROJECT_STATUS.md',
    'src/DubbedUp.Core/DubbedUp.Core.csproj',
    'src/DubbedUp.Godot/project.godot',
    'tests/DubbedUp.Core.Tests/DubbedUp.Core.Tests.csproj'
)

$missingPaths = $requiredPaths | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $repositoryRoot $_))
}

if ($missingPaths) {
    throw "Missing required repository paths: $($missingPaths -join ', ')"
}

$coreProject = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/DubbedUp.Core/DubbedUp.Core.csproj')
if ($coreProject -match 'Godot|Steamworks|FFmpeg') {
    throw 'DubbedUp.Core project contains a forbidden engine/platform dependency.'
}

$trackedFiles = git -C $repositoryRoot ls-files
$forbiddenTrackedFiles = $trackedFiles | Where-Object {
    $_ -match '(^|/)(bin|obj|\.godot|TestResults|recordings|local-data)/'
}

if ($forbiddenTrackedFiles) {
    throw "Generated or local files are tracked: $($forbiddenTrackedFiles -join ', ')"
}

Write-Output 'Repository consistency checks passed.'

