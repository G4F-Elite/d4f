namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandleDoctor(DoctorCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string[] requiredPaths =
        [
            Path.Combine(projectDirectory, "project.json"),
            Path.Combine(projectDirectory, "assets", "manifest.json"),
            Path.Combine(projectDirectory, "src")
        ];

        var failures = new List<string>();
        foreach (string requiredPath in requiredPaths)
        {
            bool exists = requiredPath.EndsWith("src", StringComparison.OrdinalIgnoreCase)
                ? Directory.Exists(requiredPath)
                : File.Exists(requiredPath);
            if (!exists)
            {
                failures.Add($"Missing required path: {requiredPath}");
            }
        }

        ToolVersionCheckResult dotnetCheck = ExecuteToolVersionCheck("dotnet", projectDirectory);
        if (!dotnetCheck.IsSuccess)
        {
            failures.Add(dotnetCheck.BuildFailureMessage("dotnet"));
        }

        ToolVersionCheckResult cmakeCheck = ExecuteToolVersionCheck("cmake", projectDirectory);
        if (!cmakeCheck.IsSuccess)
        {
            failures.Add(cmakeCheck.BuildFailureMessage("cmake"));
        }

        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                _stderr.WriteLine(failure);
            }

            return 1;
        }

        _stdout.WriteLine("Doctor checks passed.");
        return 0;
    }

    private ToolVersionCheckResult ExecuteToolVersionCheck(string executable, string workingDirectory)
    {
        try
        {
            int exitCode = _commandRunner.Run(
                executable,
                ["--version"],
                workingDirectory,
                _stdout,
                _stderr);

            return exitCode == 0
                ? ToolVersionCheckResult.Success
                : new ToolVersionCheckResult(IsSuccess: false, ExitCode: exitCode, ErrorMessage: null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new ToolVersionCheckResult(IsSuccess: false, ExitCode: null, ErrorMessage: ex.Message);
        }
    }

    private readonly record struct ToolVersionCheckResult(
        bool IsSuccess,
        int? ExitCode,
        string? ErrorMessage)
    {
        public static ToolVersionCheckResult Success { get; } = new(
            IsSuccess: true,
            ExitCode: 0,
            ErrorMessage: null);

        public string BuildFailureMessage(string executable)
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                return $"{executable} version check failed: {ErrorMessage}";
            }

            if (ExitCode.HasValue)
            {
                return $"{executable} CLI version check failed with exit code {ExitCode.Value}.";
            }

            return $"{executable} version check failed.";
        }
    }
}
