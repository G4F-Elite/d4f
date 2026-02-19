using System.Globalization;
using System.Text.Json;
using Engine.AssetPipeline;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandleMultiplayerOrchestration(MultiplayerOrchestrationCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string cliProjectPath = Path.GetFullPath(command.CliProjectPath);
        if (!File.Exists(cliProjectPath))
        {
            _stderr.WriteLine($"CLI project file does not exist: {cliProjectPath}");
            return 1;
        }

        string outputDirectory = AssetPipelineService.ResolveRelativePath(projectDirectory, command.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        OrchestratedNodeResult server = RunOrchestratedNode(
            role: "server",
            seed: command.Seed,
            projectDirectory,
            outputDirectory,
            command.Configuration,
            command.FixedDeltaSeconds,
            command.RequireNativeTransportSuccess,
            cliProjectPath);
        OrchestratedNodeResult client1 = RunOrchestratedNode(
            role: "client-1",
            seed: checked(command.Seed + 1UL),
            projectDirectory,
            outputDirectory,
            command.Configuration,
            command.FixedDeltaSeconds,
            command.RequireNativeTransportSuccess,
            cliProjectPath);
        OrchestratedNodeResult client2 = RunOrchestratedNode(
            role: "client-2",
            seed: checked(command.Seed + 2UL),
            projectDirectory,
            outputDirectory,
            command.Configuration,
            command.FixedDeltaSeconds,
            command.RequireNativeTransportSuccess,
            cliProjectPath);

        bool allSucceeded = server.ExitCode == 0 && client1.ExitCode == 0 && client2.ExitCode == 0;
        var summary = new MultiplayerOrchestrationSummary(
            GeneratedAtUtc: DateTime.UtcNow,
            Configuration: command.Configuration,
            FixedDeltaSeconds: command.FixedDeltaSeconds,
            RequireNativeTransportSuccess: command.RequireNativeTransportSuccess,
            Nodes: [server, client1, client2],
            AllSucceeded: allSucceeded);

        string summaryPath = Path.Combine(outputDirectory, "net", "multiplayer-orchestration.json");
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
        string json = JsonSerializer.Serialize(summary, ArtifactOutputWriter.SerializerOptions);
        File.WriteAllText(summaryPath, json);

        _stdout.WriteLine($"Multiplayer orchestration summary written: {summaryPath}");
        _stdout.WriteLine($"- server: exit={server.ExitCode}, runtimeTransportSucceeded={server.RuntimeTransportSucceeded}");
        _stdout.WriteLine($"- client-1: exit={client1.ExitCode}, runtimeTransportSucceeded={client1.RuntimeTransportSucceeded}");
        _stdout.WriteLine($"- client-2: exit={client2.ExitCode}, runtimeTransportSucceeded={client2.RuntimeTransportSucceeded}");

        if (!allSucceeded)
        {
            _stderr.WriteLine("Multiplayer orchestration failed: one or more node processes returned non-zero exit code.");
            return 1;
        }

        if (command.RequireNativeTransportSuccess &&
            (!server.RuntimeTransportSucceeded || !client1.RuntimeTransportSucceeded || !client2.RuntimeTransportSucceeded))
        {
            _stderr.WriteLine("Multiplayer orchestration failed: native transport did not succeed for all orchestrated nodes.");
            return 1;
        }

        return 0;
    }

    private OrchestratedNodeResult RunOrchestratedNode(
        string role,
        ulong seed,
        string projectDirectory,
        string rootOutputDirectory,
        string configuration,
        double fixedDeltaSeconds,
        bool requireNativeTransportSuccess,
        string cliProjectPath)
    {
        string nodeOutputDirectory = Path.Combine(rootOutputDirectory, role);
        Directory.CreateDirectory(nodeOutputDirectory);

        string fixedDt = fixedDeltaSeconds.ToString("0.#######", CultureInfo.InvariantCulture);
        string seedText = seed.ToString(CultureInfo.InvariantCulture);
        string requireNativeTransport = requireNativeTransportSuccess ? "true" : "false";
        string[] arguments =
        [
            "run",
            "--project",
            cliProjectPath,
            "-c",
            configuration,
            "--",
            "multiplayer",
            "demo",
            "--project",
            projectDirectory,
            "--out",
            nodeOutputDirectory,
            "--seed",
            seedText,
            "--fixed-dt",
            fixedDt,
            "--require-native-transport",
            requireNativeTransport
        ];

        int exitCode = _commandRunner.Run(
            executablePath: "dotnet",
            arguments,
            workingDirectory: Environment.CurrentDirectory,
            _stdout,
            _stderr);

        string summaryPath = Path.Combine(nodeOutputDirectory, "net", "multiplayer-demo.json");
        if (!File.Exists(summaryPath))
        {
            return new OrchestratedNodeResult(
                Role: role,
                Seed: seed,
                ExitCode: exitCode,
                SummaryPath: summaryPath,
                RuntimeTransportEnabled: false,
                RuntimeTransportSucceeded: false,
                ServerMessagesReceived: 0,
                ClientMessagesReceived: 0);
        }

        RuntimeTransportSummary runtimeTransport = ReadRuntimeTransportSummary(summaryPath);
        return new OrchestratedNodeResult(
            Role: role,
            Seed: seed,
            ExitCode: exitCode,
            SummaryPath: summaryPath,
            RuntimeTransportEnabled: runtimeTransport.Enabled,
            RuntimeTransportSucceeded: runtimeTransport.Succeeded,
            ServerMessagesReceived: runtimeTransport.ServerMessagesReceived,
            ClientMessagesReceived: runtimeTransport.ClientMessagesReceived);
    }

    private sealed record MultiplayerOrchestrationSummary(
        DateTime GeneratedAtUtc,
        string Configuration,
        double FixedDeltaSeconds,
        bool RequireNativeTransportSuccess,
        IReadOnlyList<OrchestratedNodeResult> Nodes,
        bool AllSucceeded);

    private sealed record OrchestratedNodeResult(
        string Role,
        ulong Seed,
        int ExitCode,
        string SummaryPath,
        bool RuntimeTransportEnabled,
        bool RuntimeTransportSucceeded,
        int ServerMessagesReceived,
        int ClientMessagesReceived);
}
