$ErrorActionPreference = "Stop"

$useCasePath = "src/CloudKnowledge.Application/Document/ProcessDocument/ProcessDocumentUseCase.cs"
$diagnosticsInterfacePath = "src/CloudKnowledge.Application/Document/ProcessDocument/IDocumentProcessingDiagnostics.cs"
$workerDiagnosticsPath = "src/CloudKnowledge.Worker/LoggingDocumentProcessingDiagnostics.cs"
$workerProgramPath = "src/CloudKnowledge.Worker/Program.cs"

if (-not (Test-Path $diagnosticsInterfacePath)) {
    throw "Missing document processing diagnostics interface: $diagnosticsInterfacePath"
}

if (-not (Test-Path $workerDiagnosticsPath)) {
    throw "Missing Worker logging diagnostics implementation: $workerDiagnosticsPath"
}

$useCase = Get-Content $useCasePath -Raw
$diagnosticsInterface = Get-Content $diagnosticsInterfacePath -Raw
$workerDiagnostics = Get-Content $workerDiagnosticsPath -Raw
$workerProgram = Get-Content $workerProgramPath -Raw

$requiredStages = @(
    "blob-open",
    "blob-copy",
    "text-extract",
    "chunk",
    "embeddings",
    "save-chunks",
    "save-embeddings",
    "mark-ready"
)

foreach ($stage in $requiredStages) {
    if (-not $useCase.Contains('"' + $stage + '"')) {
        throw "ProcessDocumentUseCase does not trace required stage '$stage'."
    }
}

if (-not $diagnosticsInterface.Contains("StageStarted")) {
    throw "IDocumentProcessingDiagnostics must expose StageStarted."
}

if (-not $diagnosticsInterface.Contains("StageCompleted")) {
    throw "IDocumentProcessingDiagnostics must expose StageCompleted."
}

if (-not $workerDiagnostics.Contains("ILogger<LoggingDocumentProcessingDiagnostics>")) {
    throw "Worker diagnostics must use structured ILogger logging."
}

if (-not $workerDiagnostics.Contains("stage {Stage} started")) {
    throw "Worker diagnostics must log stage start events."
}

if (-not $workerDiagnostics.Contains("stage {Stage} completed in {ElapsedMilliseconds} ms")) {
    throw "Worker diagnostics must log stage completion elapsed time."
}

$registrationPattern = '(?s)AddSingleton\s*<\s*IDocumentProcessingDiagnostics\s*,\s*LoggingDocumentProcessingDiagnostics\s*>'
if ($workerProgram -notmatch $registrationPattern) {
    throw "Worker Program.cs must register IDocumentProcessingDiagnostics to LoggingDocumentProcessingDiagnostics."
}

Write-Host "Worker processing diagnostics contract passed."
