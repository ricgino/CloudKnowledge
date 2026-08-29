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

if (-not $content.Contains('az ad sp create', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must create the service principal when the lookup returns no result."
}

$immutableSubjectFragments = @(
    'gh api "repos/$Repository"',
    '$repositoryMetadata.owner.id',
    '$repositoryMetadata.id',
    'repo:$ownerLogin@$ownerId/$repositoryName@$repositoryId`:environment:$GitHubEnvironment'
)

foreach ($fragment in $immutableSubjectFragments) {
    if (-not $content.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "configure-github-oidc.ps1 must derive the immutable GitHub OIDC subject from repository owner/repository IDs: $fragment"
    }
}

$expectedIssuerFragment = '$issuer = "https://token.actions.githubusercontent.com"'
if (-not $content.Contains($expectedIssuerFragment, [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must use the exact GitHub OIDC issuer without a trailing slash."
}

if ($content.Contains('issuer      = "https://token.actions.githubusercontent.com/"', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must not register the GitHub OIDC issuer with a trailing slash."
}

$credentialInspectionFragments = @(
    'issuer:issuer',
    'audiences:audiences',
    '$existingIssuer',
    '$existingAudiences'
)

foreach ($fragment in $credentialInspectionFragments) {
    if (-not $content.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "configure-github-oidc.ps1 must inspect subject, issuer, and audience before deciding whether the federated credential is current: $fragment"
    }
}

if (-not $content.Contains('"federated-credential", "update"', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must update an existing federated credential when its OIDC settings are stale."
}

if (-not $content.Contains('"--federated-credential-id", $credentialName', [System.StringComparison]::Ordinal)) {
    throw "configure-github-oidc.ps1 must identify the existing federated credential when updating it."
}

Write-Host "GitHub OIDC service-principal and immutable-credential contract passed."
