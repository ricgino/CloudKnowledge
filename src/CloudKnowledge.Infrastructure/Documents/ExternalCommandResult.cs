namespace CloudKnowledge.Infrastructure.Documents;

public sealed record ExternalCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
