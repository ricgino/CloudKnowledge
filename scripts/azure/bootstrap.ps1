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

$resourceGroup = terraform -chdir=$bootstrapPath output -raw resource_group_name
$resourceGroupLocation = terraform -chdir=$bootstrapPath output -raw resource_group_location
$workloadLocation = terraform -chdir=$bootstrapPath output -raw workload_location
$acrName = terraform -chdir=$bootstrapPath output -raw acr_name
$acrLoginServer = terraform -chdir=$bootstrapPath output -raw acr_login_server
$stateAccount = terraform -chdir=$bootstrapPath output -raw terraform_state_storage_account_name
$stateContainer = terraform -chdir=$bootstrapPath output -raw terraform_state_container_name
$stateKey = terraform -chdir=$bootstrapPath output -raw terraform_state_key

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
