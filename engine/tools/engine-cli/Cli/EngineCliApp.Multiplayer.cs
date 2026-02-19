using System.Text.Json;
using Engine.AssetPipeline;

namespace Engine.Cli;

public sealed partial class EngineCliApp
{
    private int HandleMultiplayerDemo(MultiplayerDemoCommand command)
    {
        string projectDirectory = Path.GetFullPath(command.ProjectDirectory);
        if (!Directory.Exists(projectDirectory))
        {
            _stderr.WriteLine($"Project directory does not exist: {projectDirectory}");
            return 1;
        }

        string outputDirectory = AssetPipelineService.ResolveRelativePath(projectDirectory, command.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);
        MultiplayerDemoArtifactOutput artifacts = MultiplayerDemoArtifactGenerator.Generate(
            outputDirectory,
            command.Seed,
            command.FixedDeltaSeconds);

        string summaryPath = Path.Combine(outputDirectory, artifacts.SummaryRelativePath.Replace('/', Path.DirectorySeparatorChar));
        RuntimeTransportSummary runtimeTransport = ReadRuntimeTransportSummary(summaryPath);

        _stdout.WriteLine($"Multiplayer runtime demo artifacts created: {outputDirectory}");
        _stdout.WriteLine($"- Summary: {artifacts.SummaryRelativePath}");
        _stdout.WriteLine($"- Net profile: {artifacts.ProfileLogRelativePath}");
        _stdout.WriteLine($"- Snapshot binary: {artifacts.SnapshotBinaryRelativePath}");
        _stdout.WriteLine($"- RPC binary: {artifacts.RpcBinaryRelativePath}");
        _stdout.WriteLine(
            $"Native transport: enabled={runtimeTransport.Enabled}, succeeded={runtimeTransport.Succeeded}, serverMessages={runtimeTransport.ServerMessagesReceived}, clientMessages={runtimeTransport.ClientMessagesReceived}.");

        if (command.RequireNativeTransportSuccess && (!runtimeTransport.Enabled || !runtimeTransport.Succeeded))
        {
            _stderr.WriteLine("Multiplayer runtime demo failed: native transport was required but did not succeed.");
            return 1;
        }

        return 0;
    }

    private static RuntimeTransportSummary ReadRuntimeTransportSummary(string summaryPath)
    {
        string json = File.ReadAllText(summaryPath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("runtimeTransport", out JsonElement runtimeTransport) || runtimeTransport.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Multiplayer runtime summary is missing object property 'runtimeTransport'.");
        }

        bool enabled = ReadRequiredBool(runtimeTransport, "enabled", "Multiplayer runtime summary runtimeTransport");
        bool succeeded = ReadRequiredBool(runtimeTransport, "succeeded", "Multiplayer runtime summary runtimeTransport");
        int serverMessages = ReadRequiredInt(runtimeTransport, "serverMessagesReceived", "Multiplayer runtime summary runtimeTransport");
        int clientMessages = ReadRequiredInt(runtimeTransport, "clientMessagesReceived", "Multiplayer runtime summary runtimeTransport");
        if (serverMessages < 0 || clientMessages < 0)
        {
            throw new InvalidDataException("Multiplayer runtime summary runtimeTransport message counters cannot be negative.");
        }

        return new RuntimeTransportSummary(
            Enabled: enabled,
            Succeeded: succeeded,
            ServerMessagesReceived: serverMessages,
            ClientMessagesReceived: clientMessages);
    }
}
