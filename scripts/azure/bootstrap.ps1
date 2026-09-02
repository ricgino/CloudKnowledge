[CmdletBinding()]
param(
    [string]$SubscriptionId = "",
    [string]$ResourceGroupLocation = "westeurope",
    [string]$WorkloadLocation = "italynorth",
    [string]$ResourcePrefix = "cloudknowledge",
    [string]$Environment = "demo",
    [switch]$AutoApprove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Command {
    param([Parameter(Mandatory)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$Command $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

function Get-RequiredTerraformOutput {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$Name
    )

    $value = terraform "-chdir=$Directory" output -raw $Name

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        throw "Terraform output '$Name' could not be read from '$Directory'."
    }

    return ([string]$value).Trim()
}

function Ensure-AzureProviderRegistration {
    param(
        [Parameter(Mandatory)][string]$Namespace,
        [Parameter(Mandatory)][string]$SubscriptionId
    )

    $registrationState = az provider show `
        --namespace $Namespace `
        --subscription $SubscriptionId `
        --query registrationState `
        --output tsv `
        --only-show-errors

    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect Azure resource provider '$Namespace'."
    }

    if ([string]::Equals(
            [string]$registrationState,
            "Registered",
            [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "Azure provider already registered: $Namespace"
        return
    }

    Write-Host "Registering Azure provider: $Namespace"

    Invoke-Native "az" @(
        "provider", "register",
        "--namespace", $Namespace,
        "--subscription", $SubscriptionId,
        "--wait",
        "--only-show-errors"
    )

    $registrationState = az provider show `
        --namespace $Namespace `
        --subscription $SubscriptionId `
        --query registrationState `
        --output tsv `
        --only-show-errors

    if ($LASTEXITCODE -ne 0 -or
        -not [string]::Equals(
            [string]$registrationState,
            "Registered",
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Azure resource provider '$Namespace' did not reach Registered state. Current state: '$registrationState'."
    }

    Write-Host "Azure provider registered: $Namespace"
}

Assert-Command "az"
Assert-Command "terraform"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$bootstrapPath = Join-Path $repoRoot "infra\azure\bootstrap"
$planPath = Join-Path $bootstrapPath "bootstrap.tfplan"

if (-not (Test-Path $bootstrapPath)) {
    throw "Terraform bootstrap directory was not found at '$bootstrapPath'."
}

if ([string]::IsNullOrWhiteSpace($SubscriptionId)) {
    $account = az account show --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $null -eq $account) {
        throw "No active Azure CLI account was found. Run 'az login' first."
    }

    $SubscriptionId = [string]$account.id
}
else {
    Invoke-Native "az" @("account", "set", "--subscription", $SubscriptionId)
    $account = az account show --output json | ConvertFrom-Json
}

Write-Host "Azure account:          $($account.name)"
Write-Host "Subscription:           $SubscriptionId"
Write-Host "Resource group region:  $ResourceGroupLocation"
Write-Host "Workload region:        $WorkloadLocation"
Write-Host "Prefix:                 $ResourcePrefix"
Write-Host "Environment:            $Environment"
Write-Host ""

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

Write-Host "Ensuring required Azure resource providers are registered..."

foreach ($providerNamespace in $requiredProviders) {
    Ensure-AzureProviderRegistration `
        -Namespace $providerNamespace `
        -SubscriptionId $SubscriptionId
}

Write-Host "All required Azure resource providers are registered."
Write-Host ""

Invoke-Native "terraform" @(
    "-chdir=$bootstrapPath",
    "init",
    "-lockfile=readonly"
)

Invoke-Native "terraform" @(
    "-chdir=$bootstrapPath",
    "plan",
    "-out=$planPath",
    "-var=subscription_id=$SubscriptionId",
    "-var=resource_group_location=$ResourceGroupLocation",
    "-var=workload_location=$WorkloadLocation",
    "-var=resource_prefix=$ResourcePrefix",
    "-var=environment=$Environment"
)

if (-not $AutoApprove) {
    Write-Host ""
    Write-Host "Terraform plan created. Review the region and resource actions above."
    $confirmation = Read-Host "Type DEPLOY to apply the plan"

    if ($confirmation -cne "DEPLOY") {
        Remove-Item $planPath -ErrorAction SilentlyContinue
        Write-Host "Bootstrap cancelled. No Terraform apply was executed."
        exit 0
    }
}

try {
    Invoke-Native "terraform" @(
        "-chdir=$bootstrapPath",
        "apply",
        $planPath
    )
}
finally {
    Remove-Item $planPath -ErrorAction SilentlyContinue
}

$resourceGroup = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "resource_group_name"
$resourceGroupLocation = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "resource_group_location"
$workloadLocation = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "workload_location"
$acrName = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "acr_name"
$acrLoginServer = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "acr_login_server"
$stateAccount = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "terraform_state_storage_account_name"
$stateContainer = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "terraform_state_container_name"
$stateKey = Get-RequiredTerraformOutput -Directory $bootstrapPath -Name "terraform_state_key"

Write-Host ""
Write-Host "Azure bootstrap completed."
Write-Host "Resource group:         $resourceGroup"
Write-Host "Resource group region:  $resourceGroupLocation"
Write-Host "Workload region:        $workloadLocation"
Write-Host "ACR:                    $acrName"
Write-Host "ACR login server:       $acrLoginServer"
Write-Host "TF state account:       $stateAccount"
Write-Host "TF state container:     $stateContainer"
Write-Host "TF state key:           $stateKey"
Write-Host ""
Write-Host "Next: run ./scripts/azure/configure-github-oidc.ps1"
