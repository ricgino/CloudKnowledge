Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$platformPath = Join-Path $repoRoot "infra\azure\platform\main.tf"
$platform = Get-Content -Raw $platformPath

$environmentMatch = [regex]::Match(
    $platform,
    'resource\s+"azurerm_container_app_environment"\s+"cloudknowledge"\s*\{(?<body>[\s\S]*?)\n\}')

if (-not $environmentMatch.Success) {
    throw "Azure platform is missing the CloudKnowledge Container Apps Environment resource."
}

$environmentBody = $environmentMatch.Groups["body"].Value

if (-not $environmentBody.Contains(
    'log_analytics_workspace_id = azurerm_log_analytics_workspace.cloudknowledge.id',
    [System.StringComparison]::Ordinal)) {
    throw "Container Apps Environment must reference the CloudKnowledge Log Analytics workspace."
}

if (-not $environmentBody.Contains(
    'logs_destination           = "log-analytics"',
    [System.StringComparison]::Ordinal)) {
    throw "Container Apps Environment must explicitly use logs_destination = log-analytics when a Log Analytics workspace ID is configured."
}

Write-Host "Azure platform deployment contract passed."
