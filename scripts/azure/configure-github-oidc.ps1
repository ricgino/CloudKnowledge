[CmdletBinding()]
param(
    [string]$Repository = "ricgino/CloudKnowledge",
    [string]$GitHubEnvironment = "azure-demo",
    [string]$ApplicationDisplayName = "CloudKnowledge GitHub Deploy",
    [string]$AzureAdTenantId = "24761888-7338-4aac-9cca-eede0c9651b2",
    [string]$AzureAdApiClientId = "3553ddee-92f1-464e-a409-4395bddb3898"
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
        throw "Terraform output '$Name' could not be read from '$Directory'. Run bootstrap.ps1 first."
    }

    return $value.Trim()
}

function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory)][string]$PrincipalObjectId,
        [Parameter(Mandatory)][string]$RoleName,
        [Parameter(Mandatory)][string]$Scope
    )

    $existing = az role assignment list `
        --assignee-object-id $PrincipalObjectId `
        --scope $Scope `
        --role $RoleName `
        --query "[0].id" `
        --output tsv

    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect role '$RoleName' at scope '$Scope'."
    }

    if ([string]::IsNullOrWhiteSpace($existing)) {
        Write-Host "Granting '$RoleName'..."
        Invoke-Native "az" @(
            "role", "assignment", "create",
            "--assignee-object-id", $PrincipalObjectId,
            "--assignee-principal-type", "ServicePrincipal",
            "--role", $RoleName,
            "--scope", $Scope,
            "--output", "none"
        )
    }
    else {
        Write-Host "Role '$RoleName' already exists."
    }
}

function Set-GitHubEnvironmentVariable {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Value
    )

    Invoke-Native "gh" @(
        "variable", "set", $Name,
        "--repo", $Repository,
        "--env", $GitHubEnvironment,
        "--body", $Value
    )
}

Assert-Command "az"
Assert-Command "gh"
Assert-Command "terraform"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$bootstrapPath = Join-Path $repoRoot "infra\azure\bootstrap"

Invoke-Native "gh" @("auth", "status")

$repositoryMetadataJson = gh api "repos/$Repository"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryMetadataJson)) {
    throw "Could not read GitHub repository metadata for '$Repository'."
}

$repositoryMetadata = $repositoryMetadataJson | ConvertFrom-Json
$ownerLogin = [string]$repositoryMetadata.owner.login
$ownerId = [string]$repositoryMetadata.owner.id
$repositoryName = [string]$repositoryMetadata.name
$repositoryId = [string]$repositoryMetadata.id

if (
    [string]::IsNullOrWhiteSpace($ownerLogin) -or
    [string]::IsNullOrWhiteSpace($ownerId) -or
    [string]::IsNullOrWhiteSpace($repositoryName) -or
    [string]::IsNullOrWhiteSpace($repositoryId)
) {
    throw "GitHub repository metadata is missing the owner/repository IDs required for immutable OIDC subjects."
}

$account = az account show --output json | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $null -eq $account) {
    throw "No active Azure CLI account was found. Run 'az login' first."
}

$subscriptionId = [string]$account.id
$tenantId = [string]$account.tenantId

$resourceGroup = Get-RequiredTerraformOutput $bootstrapPath "resource_group_name"
$acrName = Get-RequiredTerraformOutput $bootstrapPath "acr_name"
$stateAccount = Get-RequiredTerraformOutput $bootstrapPath "terraform_state_storage_account_name"
$stateContainer = Get-RequiredTerraformOutput $bootstrapPath "terraform_state_container_name"
$stateKey = Get-RequiredTerraformOutput $bootstrapPath "terraform_state_key"

$resourceGroupId = az group show `
    --name $resourceGroup `
    --query id `
    --output tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($resourceGroupId)) {
    throw "Resource group '$resourceGroup' was not found."
}

$acrId = az acr show `
    --name $acrName `
    --resource-group $resourceGroup `
    --query id `
    --output tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($acrId)) {
    throw "ACR '$acrName' was not found."
}

$stateAccountId = az storage account show `
    --name $stateAccount `
    --resource-group $resourceGroup `
    --query id `
    --output tsv

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($stateAccountId)) {
    throw "Terraform state account '$stateAccount' was not found."
}

Write-Host "Ensuring GitHub environment '$GitHubEnvironment'..."
Invoke-Native "gh" @(
    "api",
    "--method", "PUT",
    "repos/$Repository/environments/$GitHubEnvironment",
    "--silent"
)

$appJson = az ad app list `
    --display-name $ApplicationDisplayName `
    --query "[0].{appId:appId,id:id,displayName:displayName}" `
    --output json

if ($LASTEXITCODE -ne 0) {
    throw "Could not query Microsoft Entra applications."
}

$app = $appJson | ConvertFrom-Json

if ($null -eq $app -or [string]::IsNullOrWhiteSpace([string]$app.appId)) {
    Write-Host "Creating Microsoft Entra application '$ApplicationDisplayName'..."
    $app = az ad app create `
        --display-name $ApplicationDisplayName `
        --query "{appId:appId,id:id,displayName:displayName}" `
        --output json | ConvertFrom-Json

    if ($LASTEXITCODE -ne 0 -or $null -eq $app) {
        throw "Microsoft Entra application creation failed."
    }
}
else {
    Write-Host "Reusing Microsoft Entra application '$ApplicationDisplayName'."
}

$appId = [string]$app.appId

$servicePrincipalJson = az ad sp list `
    --filter "appId eq '$appId'" `
    --query "[0].{id:id,appId:appId}" `
    --output json

if ($LASTEXITCODE -ne 0) {
    throw "Could not query the service principal for application '$appId'."
}

$servicePrincipal = $servicePrincipalJson | ConvertFrom-Json

if ($null -eq $servicePrincipal -or [string]::IsNullOrWhiteSpace([string]$servicePrincipal.id)) {
    Write-Host "Creating service principal..."
    $servicePrincipal = az ad sp create `
        --id $appId `
        --query "{id:id,appId:appId}" `
        --output json | ConvertFrom-Json

    if ($LASTEXITCODE -ne 0 -or $null -eq $servicePrincipal) {
        throw "Service principal creation failed."
    }
}
else {
    Write-Host "Reusing existing service principal."
}

$principalObjectId = [string]$servicePrincipal.id
$credentialName = "github-$($GitHubEnvironment -replace '[^A-Za-z0-9-]', '-')"
$subject = "repo:$ownerLogin@$ownerId/$repositoryName@$repositoryId`:environment:$GitHubEnvironment"
$issuer = "https://token.actions.githubusercontent.com"
$audience = "api://AzureADTokenExchange"

$existingCredentialJson = az ad app federated-credential list `
    --id $appId `
    --query "[?name=='$credentialName'] | [0].{name:name,subject:subject,issuer:issuer,audiences:audiences}" `
    --output json

if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect federated credentials for application '$appId'."
}

$existingCredential = $existingCredentialJson | ConvertFrom-Json
$existingSubject = if ($null -ne $existingCredential) { [string]$existingCredential.subject } else { "" }
$existingIssuer = if ($null -ne $existingCredential) { [string]$existingCredential.issuer } else { "" }
[object[]]$existingAudiences = @()
if ($null -ne $existingCredential -and $null -ne $existingCredential.audiences) {
    [object[]]$existingAudiences = @($existingCredential.audiences)
}
$hasExpectedAudience = $existingAudiences.Count -eq 1 -and [string]::Equals(
    [string]$existingAudiences[0],
    $audience,
    [System.StringComparison]::Ordinal
)
$credentialIsCurrent =
    $null -ne $existingCredential -and
    -not [string]::IsNullOrWhiteSpace([string]$existingCredential.name) -and
    [string]::Equals($existingSubject, $subject, [System.StringComparison]::Ordinal) -and
    [string]::Equals($existingIssuer, $issuer, [System.StringComparison]::Ordinal) -and
    $hasExpectedAudience

if ($null -eq $existingCredential -or [string]::IsNullOrWhiteSpace([string]$existingCredential.name)) {
    Write-Host "Creating federated credential '$credentialName'..."

    $credential = [ordered]@{
        name        = $credentialName
        issuer      = $issuer
        subject     = $subject
        description = "CloudKnowledge GitHub Actions deployment through $GitHubEnvironment"
        audiences   = @($audience)
    }

    $credentialFile = Join-Path ([System.IO.Path]::GetTempPath()) "cloudknowledge-federated-credential-$([Guid]::NewGuid()).json"

    try {
        $credential | ConvertTo-Json -Depth 4 | Set-Content -Path $credentialFile -Encoding utf8
        Invoke-Native "az" @(
            "ad", "app", "federated-credential", "create",
            "--id", $appId,
            "--parameters", $credentialFile,
            "--output", "none"
        )
    }
    finally {
        Remove-Item $credentialFile -ErrorAction SilentlyContinue
    }
}
elseif (-not $credentialIsCurrent) {
    Write-Host "Updating federated credential '$credentialName' to the expected GitHub OIDC settings..."

    $credential = [ordered]@{
        issuer      = $issuer
        subject     = $subject
        description = "CloudKnowledge GitHub Actions deployment through $GitHubEnvironment"
        audiences   = @($audience)
    }

    $credentialFile = Join-Path ([System.IO.Path]::GetTempPath()) "cloudknowledge-federated-credential-$([Guid]::NewGuid()).json"

    try {
        $credential | ConvertTo-Json -Depth 4 | Set-Content -Path $credentialFile -Encoding utf8
        Invoke-Native "az" @(
            "ad", "app", "federated-credential", "update",
            "--id", $appId,
            "--federated-credential-id", $credentialName,
            "--parameters", $credentialFile,
            "--output", "none"
        )
    }
    finally {
        Remove-Item $credentialFile -ErrorAction SilentlyContinue
    }
}
else {
    Write-Host "Federated credential '$credentialName' already has the expected GitHub OIDC settings."
}

Ensure-RoleAssignment $principalObjectId "Contributor" $resourceGroupId
Ensure-RoleAssignment $principalObjectId "Role Based Access Control Administrator" $acrId
Ensure-RoleAssignment $principalObjectId "AcrPush" $acrId
Ensure-RoleAssignment $principalObjectId "Storage Blob Data Contributor" $stateAccountId

Write-Host "Setting GitHub environment variables..."
Set-GitHubEnvironmentVariable "AZURE_CLIENT_ID" $appId
Set-GitHubEnvironmentVariable "AZURE_TENANT_ID" $tenantId
Set-GitHubEnvironmentVariable "AZURE_SUBSCRIPTION_ID" $subscriptionId
Set-GitHubEnvironmentVariable "AZURE_RESOURCE_GROUP" $resourceGroup
Set-GitHubEnvironmentVariable "AZURE_ACR_NAME" $acrName
Set-GitHubEnvironmentVariable "AZURE_TF_STATE_STORAGE_ACCOUNT" $stateAccount
Set-GitHubEnvironmentVariable "AZURE_TF_STATE_CONTAINER" $stateContainer
Set-GitHubEnvironmentVariable "AZURE_TF_STATE_KEY" $stateKey
Set-GitHubEnvironmentVariable "AZURE_AD_TENANT_ID" $AzureAdTenantId
Set-GitHubEnvironmentVariable "AZURE_AD_API_CLIENT_ID" $AzureAdApiClientId

Write-Host ""
Write-Host "GitHub OIDC configuration completed."
Write-Host "Application ID: $appId"
Write-Host "OIDC issuer:    $issuer"
Write-Host "OIDC subject:   $subject"
Write-Host "OIDC audience:  $audience"
Write-Host "Environment:    $GitHubEnvironment"
Write-Host ""
Write-Host "Before the first deployment, configure these GitHub environment secrets:"
Write-Host "  POSTGRES_ADMIN_PASSWORD"
Write-Host "  OPENAI_API_KEY"
Write-Host ""
Write-Host "Secret commands (values are prompted interactively):"
Write-Host "  gh secret set POSTGRES_ADMIN_PASSWORD --repo $Repository --env $GitHubEnvironment"
Write-Host "  gh secret set OPENAI_API_KEY --repo $Repository --env $GitHubEnvironment"
