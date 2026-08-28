using System.Diagnostics;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class SystemExternalCommandRunner
    : IExternalCommandRunner
{
    public ExternalCommandResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                $"Unable to start external command '{fileName}'.");
        }

        var outputTask =
            process.StandardOutput.ReadToEndAsync(
                cancellationToken);

        var errorTask =
            process.StandardError.ReadToEndAsync(
                cancellationToken);

        try
        {
            process.WaitForExitAsync(
                    cancellationToken)
                .GetAwaiter()
                .GetResult();

            Task.WhenAll(
                    outputTask,
                    errorTask)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }

            throw;
        }

        return new ExternalCommandResult(
            process.ExitCode,
            outputTask.Result,
            errorTask.Result);
    }
}
