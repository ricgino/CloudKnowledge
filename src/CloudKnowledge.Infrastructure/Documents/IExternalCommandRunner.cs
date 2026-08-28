namespace CloudKnowledge.Infrastructure.Documents;

public interface IExternalCommandRunner
{
    ExternalCommandResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}
