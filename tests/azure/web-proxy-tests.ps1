Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$nginxPath = Join-Path $repoRoot "src\CloudKnowledge.Web\nginx.conf.template"
$nginx = Get-Content -Raw $nginxPath

$proxyLocations = [regex]::Matches(
    $nginx,
    'location\s+(?:=\s+)?[^\{]+\{(?<body>[\s\S]*?proxy_pass\s+\$\{API_UPSTREAM\};[\s\S]*?)\n\s*\}')

if ($proxyLocations.Count -ne 3) {
    throw "Expected exactly 3 Nginx API proxy locations, found $($proxyLocations.Count)."
}

foreach ($location in $proxyLocations) {
    $body = $location.Groups["body"].Value

    if (-not $body.Contains(
        'proxy_ssl_server_name on;',
        [System.StringComparison]::Ordinal)) {
        throw "Every HTTPS API proxy location must enable proxy_ssl_server_name so Azure Container Apps receives SNI."
    }

    if (-not $body.Contains(
        'proxy_set_header Host $proxy_host;',
        [System.StringComparison]::Ordinal)) {
        throw "Every API proxy location must forward the upstream host instead of the public Web host."
    }
}

Write-Host "CloudKnowledge Web internal API proxy contract passed."
