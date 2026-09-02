[CmdletBinding()]
param(
    [string]$SubscriptionId = "",
    [string]$Location = "italynorth",
    [string[]]$FallbackLocations = @(
        "swedencentral",
        "northeurope",
        "francecentral",
        "germanywestcentral",
        "eastus2",
        "northcentralus"
    ),
    [string]$EmbeddingModel = "text-embedding-3-small",
    [string[]]$AnswerCandidates = @(
        "gpt-4.1-mini",
        "gpt-4o-mini",
        "gpt-4.1-nano"
    )
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

function Get-PropertyValue {
    param(
        [Parameter(Mandatory)]$Object,
        [Parameter(Mandatory)][string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Convert-CatalogEntry {
    param([Parameter(Mandatory)]$Entry)

    $model = Get-PropertyValue -Object $Entry -Name "model"
    if ($null -eq $model) {
        return $null
    }

    $name = [string](Get-PropertyValue -Object $model -Name "name")
    $version = [string](Get-PropertyValue -Object $model -Name "version")
    $format = [string](Get-PropertyValue -Object $model -Name "format")
    $lifecycleStatus = [string](Get-PropertyValue -Object $model -Name "lifecycleStatus")

    if ([string]::IsNullOrWhiteSpace($lifecycleStatus)) {
        $lifecycleStatus = [string](Get-PropertyValue -Object $Entry -Name "lifecycleStatus")
    }

    $skuObjects = Get-PropertyValue -Object $model -Name "skus"
    $skuNames = @()

    foreach ($sku in @($skuObjects)) {
        $skuName = [string](Get-PropertyValue -Object $sku -Name "name")
        if (-not [string]::IsNullOrWhiteSpace($skuName)) {
            $skuNames += $skuName
        }
    }

    if ([string]::IsNullOrWhiteSpace($name)) {
        return $null
    }

    return [pscustomobject]@{
        Name            = $name
        Version         = $version
        Format          = $format
        LifecycleStatus = $lifecycleStatus
        Skus            = @($skuNames | Sort-Object -Unique)
    }
}

function Select-DeploymentCandidate {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]]$Catalog,

        [Parameter(Mandatory)][string]$ModelName
    )

    if ($Catalog.Count -eq 0) {
        return $null
    }

    $preferredSkus = @("GlobalStandard", "Standard", "DataZoneStandard")
    $matches = @(
        $Catalog |
            Where-Object {
                $_.Name -eq $ModelName -and
                ($_.Format -eq "OpenAI" -or [string]::IsNullOrWhiteSpace($_.Format)) -and
                $_.LifecycleStatus -ne "Deprecated"
            } |
            Sort-Object Version -Descending
    )

    foreach ($match in $matches) {
        foreach ($preferredSku in $preferredSkus) {
            if ($match.Skus -contains $preferredSku) {
                return [pscustomobject]@{
                    Name            = $match.Name
                    Version         = $match.Version
                    DeploymentSku   = $preferredSku
                    LifecycleStatus = $match.LifecycleStatus
                    AvailableSkus   = $match.Skus
                }
            }
        }
    }

    if ($matches.Count -gt 0) {
        $match = $matches[0]
        return [pscustomobject]@{
            Name            = $match.Name
            Version         = $match.Version
            DeploymentSku   = ""
            LifecycleStatus = $match.LifecycleStatus
            AvailableSkus   = $match.Skus
        }
    }

    return $null
}

function Get-DiscoveryForLocation {
    param(
        [Parameter(Mandatory)][string]$CandidateLocation,
        [Parameter(Mandatory)][string]$SubscriptionId,
        [Parameter(Mandatory)][string]$EmbeddingModel,
        [Parameter(Mandatory)][string[]]$AnswerCandidates
    )

    Write-Host "Checking OpenAI account SKUs in '$CandidateLocation'..."

    # Read-only catalog command: az cognitiveservices account list-skus
    $accountSkuJson = az cognitiveservices account list-skus `
        --kind OpenAI `
        --location $CandidateLocation `
        --subscription $SubscriptionId `
        --output json `
        --only-show-errors

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Could not list OpenAI account SKUs in '$CandidateLocation'; trying next region."
        return $null
    }

    $accountSkus = @($accountSkuJson | ConvertFrom-Json)
    Write-Host "OpenAI account SKU records returned: $($accountSkus.Count)"

    if ($accountSkus.Count -eq 0) {
        Write-Host "No OpenAI account SKU records returned; trying next region."
        return $null
    }

    Write-Host "Reading model catalog for '$CandidateLocation'..."

    # Read-only catalog command: az cognitiveservices model list
    $catalogJson = az cognitiveservices model list `
        --location $CandidateLocation `
        --subscription $SubscriptionId `
        --output json `
        --only-show-errors

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Could not read the model catalog for '$CandidateLocation'; trying next region."
        return $null
    }

    if ([string]::IsNullOrWhiteSpace([string]$catalogJson)) {
        $rawCatalog = @()
    }
    else {
        $rawCatalog = @($catalogJson | ConvertFrom-Json)
    }

    $catalog = @(
        foreach ($entry in $rawCatalog) {
            $converted = Convert-CatalogEntry -Entry $entry
            if ($null -ne $converted) {
                $converted
            }
        }
    )

    Write-Host "Model catalog entries returned: $($catalog.Count)"

    if ($catalog.Count -eq 0) {
        Write-Host "No model catalog entries returned; trying next region."
        return $null
    }

    $embedding = Select-DeploymentCandidate -Catalog $catalog -ModelName $EmbeddingModel
    if ($null -eq $embedding) {
        Write-Host "Embedding model '$EmbeddingModel' is unavailable in '$CandidateLocation'; trying next region."
        return $null
    }

    $answer = $null
    foreach ($candidateName in $AnswerCandidates) {
        $candidate = Select-DeploymentCandidate -Catalog $catalog -ModelName $candidateName
        if ($null -ne $candidate) {
            $answer = $candidate
            break
        }
    }

    if ($null -eq $answer) {
        Write-Host "None of the preferred answer models are available in '$CandidateLocation'; trying next region."
        return $null
    }

    return [pscustomobject]@{
        Location              = $CandidateLocation
        AccountSkuRecordCount = $accountSkus.Count
        ModelCatalogCount     = $catalog.Count
        Embedding             = $embedding
        Answer                = $answer
    }
}

Assert-Command "az"

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

Write-Host "Azure AI discovery"
Write-Host "Subscription: $SubscriptionId"
Write-Host "Account:      $($account.name)"
Write-Host "Preferred:    $Location"
Write-Host ""

Ensure-AzureProviderRegistration `
    -Namespace "Microsoft.CognitiveServices" `
    -SubscriptionId $SubscriptionId

$locationsToTry = @(
    @($Location) + @($FallbackLocations) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -Unique
)

Write-Host ""
Write-Host "Regions to scan: $($locationsToTry -join ', ')"
Write-Host ""

$selected = $null
$scannedLocations = @()

foreach ($candidateLocation in $locationsToTry) {
    $candidateLocation = $candidateLocation.Trim().ToLowerInvariant()
    $scannedLocations += $candidateLocation

    Write-Host "--- $candidateLocation ---"
    $discovery = Get-DiscoveryForLocation `
        -CandidateLocation $candidateLocation `
        -SubscriptionId $SubscriptionId `
        -EmbeddingModel $EmbeddingModel `
        -AnswerCandidates $AnswerCandidates

    if ($null -ne $discovery) {
        $selected = $discovery
        break
    }

    Write-Host ""
}

if ($null -eq $selected) {
    throw "No scanned Azure region exposed both '$EmbeddingModel' and one preferred answer model for this subscription. Scanned: $($scannedLocations -join ', '). No Azure AI account or model deployment was created."
}

$result = [pscustomobject]@{
    SubscriptionId        = $SubscriptionId
    Subscription          = [string]$account.name
    PreferredLocation     = $Location
    SelectedLocation      = $selected.Location
    ScannedLocations      = $scannedLocations
    AccountSkuRecordCount = $selected.AccountSkuRecordCount
    ModelCatalogCount     = $selected.ModelCatalogCount
    Embedding             = $selected.Embedding
    Answer                = $selected.Answer
}

Write-Host ""
Write-Host "Recommended CloudKnowledge Azure AI candidates"
Write-Host "Location:  $($selected.Location)"
Write-Host "Embedding: $($selected.Embedding.Name) | version $($selected.Embedding.Version) | SKU $($selected.Embedding.DeploymentSku)"
Write-Host "Answer:    $($selected.Answer.Name) | version $($selected.Answer.Version) | SKU $($selected.Answer.DeploymentSku)"
Write-Host ""
Write-Host "Discovery result JSON:"
$result | ConvertTo-Json -Depth 6
Write-Host ""
Write-Host "No Azure AI account or model deployment was created."
