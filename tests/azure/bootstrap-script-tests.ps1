Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$bootstrapPath = Join-Path $repoRoot "scripts\azure\bootstrap.ps1"

if (-not (Test-Path $bootstrapPath)) {
    throw "Bootstrap script was not found at '$bootstrapPath'."
}

$content = Get-Content -Raw $bootstrapPath

$requiredProviders = @(
    "Microsoft.ContainerRegistry",
    "Microsoft.Storage",
    "Microsoft.DBforPostgreSQL",
    "Microsoft.ServiceBus",
    "Microsoft.App",
    "Microsoft.OperationalInsights",
    "Microsoft.ManagedIdentity",
    "Microsoft.Authorization"
)

foreach ($provider in $requiredProviders) {
    if (-not $content.Contains($provider, [System.StringComparison]::Ordinal)) {
        throw "bootstrap.ps1 must register required Azure provider '$provider'."
    }
}

if (-not $content.Contains('"provider", "register"', [System.StringComparison]::Ordinal)) {
    throw "bootstrap.ps1 must invoke 'az provider register'."
}

if (-not $content.Contains('"--wait"', [System.StringComparison]::Ordinal)) {
    throw "bootstrap.ps1 must wait for Azure provider registration to finish before Terraform runs."
}

$registrationPosition = $content.IndexOf('"provider", "register"', [System.StringComparison]::Ordinal)
$terraformInitPosition = $content.IndexOf('"-chdir=$bootstrapPath",', [System.StringComparison]::Ordinal)

if ($registrationPosition -lt 0 -or $terraformInitPosition -lt 0 -or $registrationPosition -gt $terraformInitPosition) {
    throw "Azure provider registration must happen before Terraform initialization."
}

if ($content.Contains('terraform -chdir=$bootstrapPath output', [System.StringComparison]::Ordinal)) {
    throw "bootstrap.ps1 must not pass a literal '-chdir=`$bootstrapPath' token when reading Terraform outputs."
}

if (-not $content.Contains('terraform "-chdir=$Directory" output -raw $Name', [System.StringComparison]::Ordinal)) {
    throw "bootstrap.ps1 must read Terraform outputs through an expanded quoted -chdir argument."
}

Write-Host "Azure bootstrap script contract passed."
