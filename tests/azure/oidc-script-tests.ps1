Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$scriptPath = Join-Path $repoRoot "scripts\azure\configure-github-oidc.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "OIDC configuration script was not found at '$scriptPath'."
}

$content = Get-Content -Raw $scriptPath

if ($content.Contains('az ad sp show', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must not probe a possibly-missing service principal with 'az ad sp show' because Windows PowerShell treats the expected not-found stderr as a terminating NativeCommandError."
}

if (-not $content.Contains('az ad sp list', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must query service principals with 'az ad sp list'."
}

if (-not $content.Contains('--filter "appId eq ''$appId''"', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must filter service principals by appId so an absent service principal is represented by an empty result."
}

if (-not $content.Contains('"ad", "sp", "create"', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must create the service principal when the lookup returns no result."
}

Write-Host "GitHub OIDC service-principal lookup contract passed."
