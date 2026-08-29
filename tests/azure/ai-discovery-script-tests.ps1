Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$scriptPath = Join-Path $repoRoot "scripts\azure\discover-ai.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "Azure AI discovery script was not found at '$scriptPath'."
}

$content = Get-Content -Raw $scriptPath

$requiredFragments = @(
    '"italynorth"',
    '"swedencentral"',
    'FallbackLocations',
    '[AllowEmptyCollection()]',
    'Microsoft.CognitiveServices',
    '"provider", "register"',
    '"--wait"',
    'cognitiveservices account list-skus',
    'cognitiveservices model list',
    'text-embedding-3-small',
    'gpt-4.1-mini',
    'gpt-4o-mini',
    'gpt-4.1-nano',
    'No model catalog entries returned',
    'trying next region'
)

foreach ($fragment in $requiredFragments) {
    if (-not $content.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "discover-ai.ps1 is missing required discovery contract fragment: $fragment"
    }
}

$forbiddenFragments = @(
    'cognitiveservices account create',
    'cognitiveservices account deployment create'
)

foreach ($fragment in $forbiddenFragments) {
    if ($content.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "discover-ai.ps1 must be read-only with respect to billable AI resources; forbidden command found: $fragment"
    }
}

Write-Host "Azure AI discovery script contract passed."
