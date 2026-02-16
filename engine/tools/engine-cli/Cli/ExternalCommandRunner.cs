using System.Diagnostics;

namespace Engine.Cli;

public interface IExternalCommandRunner
{
    int Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TextWriter stdout,
        TextWriter stderr);
}

public sealed class ProcessExternalCommandRunner : IExternalCommandRunner
{
    public int Run(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TextWriter stdout,
        TextWriter stderr)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{executablePath}'.");
        }

        string stdOutText = process.StandardOutput.ReadToEnd();
        string stdErrText = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(stdOutText))
        {
            stdout.Write(stdOutText);
        }

        if (!string.IsNullOrEmpty(stdErrText))
        {
            stderr.Write(stdErrText);
        }

        return process.ExitCode;
    }
}
