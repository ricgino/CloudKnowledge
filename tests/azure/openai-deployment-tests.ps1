Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$variablesPath = Join-Path $repoRoot "infra\azure\platform\variables.tf"
$containerAppsPath = Join-Path $repoRoot "infra\azure\platform\container-apps.tf"
$workflowPath = Join-Path $repoRoot ".github\workflows\azure-deploy.yml"

$variables = Get-Content -Raw $variablesPath
$containerApps = Get-Content -Raw $containerAppsPath
$workflow = Get-Content -Raw $workflowPath

$requiredVariableFragments = @(
    'variable "ai_provider"',
    'variable "ai_endpoint"',
    'variable "ai_api_key"',
    'variable "ai_embedding_model"',
    'variable "ai_answer_model"'
)

foreach ($fragment in $requiredVariableFragments) {
    if (-not $variables.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "Azure platform variables are missing provider-neutral AI configuration: $fragment"
    }
}

$requiredContainerFragments = @(
    'name  = "Ai__Provider"',
    'value = var.ai_provider',
    'name  = "Ai__Endpoint"',
    'value = var.ai_endpoint',
    'name  = "Ai__EmbeddingModel"',
    'value = var.ai_embedding_model',
    'name  = "Ai__AnswerModel"',
    'value = var.ai_answer_model',
    'name  = "ai-api-key"',
    'value = var.ai_api_key'
)

foreach ($fragment in $requiredContainerFragments) {
    if (-not $containerApps.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "Container Apps configuration is missing direct OpenAI wiring: $fragment"
    }
}

if ($containerApps.Contains('value = "AzureOpenAI"', [System.StringComparison]::Ordinal)) {
    throw "Azure deployment must not hardcode AzureOpenAI after selecting the direct OpenAI fallback."
}

$requiredWorkflowFragments = @(
    'AI_PROVIDER: "OpenAI"',
    'AI_ENDPOINT: "https://api.openai.com/"',
    'AI_EMBEDDING_MODEL: "text-embedding-3-small"',
    'AI_ANSWER_MODEL: "gpt-4.1-nano"',
    'OPENAI_API_KEY: ${{ secrets.OPENAI_API_KEY }}',
    'TF_VAR_ai_provider:',
    'TF_VAR_ai_endpoint:',
    'TF_VAR_ai_api_key:',
    'TF_VAR_ai_embedding_model:',
    'TF_VAR_ai_answer_model:'
)

foreach ($fragment in $requiredWorkflowFragments) {
    if (-not $workflow.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "Azure deployment workflow is missing direct OpenAI configuration: $fragment"
    }
}

$obsoleteWorkflowFragments = @(
    'AZURE_OPENAI_ENDPOINT',
    'AZURE_OPENAI_API_KEY',
    'AZURE_OPENAI_EMBEDDING_DEPLOYMENT',
    'AZURE_OPENAI_ANSWER_DEPLOYMENT'
)

foreach ($fragment in $obsoleteWorkflowFragments) {
    if ($workflow.Contains($fragment, [System.StringComparison]::Ordinal)) {
        throw "Azure deployment workflow still requires obsolete Azure OpenAI setting: $fragment"
    }
}

Write-Host "Direct OpenAI Azure deployment contract passed."
