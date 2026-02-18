using System.Globalization;
using System.Text.Json;
using Engine.NativeBindings;
using Engine.Net;
using Engine.Procedural;
using Engine.Rendering;

namespace Engine.Cli;

internal sealed record MultiplayerDemoStats(
    long BytesSent,
    long BytesReceived,
    int MessagesSent,
    int MessagesReceived,
    int MessagesDropped,
    double RoundTripTimeMs,
    double LossPercent,
    double AverageSendBandwidthKbps,
    double AverageReceiveBandwidthKbps,
    double PeakSendBandwidthKbps,
    double PeakReceiveBandwidthKbps);

internal sealed record MultiplayerDemoClientStats(
    uint ClientId,
    MultiplayerDemoStats Stats);

internal sealed record MultiplayerDemoOwnershipStats(
    uint ClientId,
    int OwnedEntityCount);

internal sealed record MultiplayerDemoArtifactOutput(
    string SummaryRelativePath,
    string ProfileLogRelativePath);

internal sealed record RuntimeTransportSummary(
    bool Enabled,
    bool Succeeded,
    int ServerMessagesReceived,
    int ClientMessagesReceived);

internal sealed record MultiplayerDemoSummary(
    ulong Seed,
    uint ProceduralSeed,
    double FixedDeltaSeconds,
    int TickRateHz,
    int SimulatedTicks,
    int ConnectedClients,
    int GeneratedChunkCount,
    int ServerEntityCount,
    bool Synchronized,
    IReadOnlyList<string> SampleAssetKeys,
    MultiplayerDemoStats ServerStats,
    IReadOnlyList<MultiplayerDemoClientStats> ClientStats,
    IReadOnlyList<MultiplayerDemoOwnershipStats> OwnershipStats,
    RuntimeTransportSummary RuntimeTransport);

internal static class MultiplayerDemoArtifactGenerator
{
    private const string ComponentId = "transform";
    private const int DefaultSurfaceWidth = 64;
    private const int DefaultSurfaceHeight = 64;
    private const int SimulatedTickCount = 3;

    public static MultiplayerDemoArtifactOutput Generate(string outputDirectory, ulong seed, double fixedDeltaSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (fixedDeltaSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds), "Fixed delta must be greater than zero.");
        }

        MultiplayerDemoSummary summary = BuildSummary(seed, fixedDeltaSeconds);
        string summaryRelativePath = Path.Combine("net", "multiplayer-demo.json");
        string summaryFullPath = Path.Combine(outputDirectory, summaryRelativePath);

        string? directory = Path.GetDirectoryName(summaryFullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(summary, ArtifactOutputWriter.SerializerOptions);
        File.WriteAllText(summaryFullPath, json);

        string profileLogRelativePath = Path.Combine("net", "multiplayer-profile.log");
        string profileLogFullPath = Path.Combine(outputDirectory, profileLogRelativePath);
        WriteProfileLog(profileLogFullPath, summary);

        return new MultiplayerDemoArtifactOutput(
            SummaryRelativePath: NormalizePath(summaryRelativePath),
            ProfileLogRelativePath: NormalizePath(profileLogRelativePath));
    }

    private static MultiplayerDemoSummary BuildSummary(ulong seed, double fixedDeltaSeconds)
    {
        uint proceduralSeed = unchecked((uint)seed);
        int tickRate = ResolveTickRate(fixedDeltaSeconds);
        LevelGenResult level = LevelGenerator.Generate(new LevelGenOptions(
            Seed: proceduralSeed,
            TargetNodes: 16,
            Density: 0.72f,
            Danger: 0.45f,
            Complexity: 0.58f));
        IReadOnlyList<LevelMeshChunk> chunks = level.MeshChunks;

        InMemoryNetSession session = CreateSession(tickRate);
        session.RegisterReplicatedComponent(ComponentId);
        uint firstClientId = session.ConnectClient();
        uint secondClientId = session.ConnectClient();

        var firstReplicator = new NetProceduralChunkReplicator(NoopRenderingFacade.Instance);
        var secondReplicator = new NetProceduralChunkReplicator(NoopRenderingFacade.Instance);
        NetProceduralChunkApplyResult firstResult;
        NetProceduralChunkApplyResult secondResult;
        NetSnapshot firstSnapshot = default!;
        NetSnapshot secondSnapshot = default!;
        try
        {
            UpsertProceduralEntities(session, chunks, proceduralSeed, firstClientId, secondClientId);

            firstResult = default!;
            secondResult = default!;
            for (int tick = 0; tick < SimulatedTickCount; tick++)
            {
                session.Pump();
                firstSnapshot = session.GetClientSnapshot(firstClientId);
                secondSnapshot = session.GetClientSnapshot(secondClientId);
                firstResult = firstReplicator.Apply(firstSnapshot);
                secondResult = secondReplicator.Apply(secondSnapshot);
            }
        }
        finally
        {
            firstReplicator.Dispose();
            secondReplicator.Dispose();
        }

        bool synchronized = AreProceduralSnapshotsEquivalent(firstResult.ActiveEntities, secondResult.ActiveEntities)
            && AreNetSnapshotsEquivalent(firstSnapshot, secondSnapshot);
        if (!synchronized)
        {
            throw new InvalidDataException("Multiplayer demo produced divergent client snapshots.");
        }

        RuntimeTransportSummary runtimeTransport = RunRuntimeTransportDemoIfAvailable();

        string[] sampleAssetKeys = firstResult.ActiveEntities
            .Select(static x => x.AssetKey)
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToArray();

        return new MultiplayerDemoSummary(
            Seed: seed,
            ProceduralSeed: proceduralSeed,
            FixedDeltaSeconds: fixedDeltaSeconds,
            TickRateHz: tickRate,
            SimulatedTicks: SimulatedTickCount,
            ConnectedClients: session.ConnectedClientCount,
            GeneratedChunkCount: chunks.Count,
            ServerEntityCount: firstResult.ActiveEntities.Count,
            Synchronized: synchronized,
            SampleAssetKeys: sampleAssetKeys,
            ServerStats: ToStats(session.GetServerStats()),
            ClientStats:
            [
                new MultiplayerDemoClientStats(firstClientId, ToStats(session.GetClientStats(firstClientId))),
                new MultiplayerDemoClientStats(secondClientId, ToStats(session.GetClientStats(secondClientId)))
            ],
            OwnershipStats: BuildOwnershipStats(firstSnapshot, [firstClientId, secondClientId]),
            RuntimeTransport: runtimeTransport);
    }

    private static RuntimeTransportSummary RunRuntimeTransportDemoIfAvailable()
    {
        string? previousPeerId = Environment.GetEnvironmentVariable("DFF_NET_LOCAL_PEER_ID");
        try
        {
            using NativeFacadeSet? server = TryCreateNativePeer(100u);
            using NativeFacadeSet? clientA = TryCreateNativePeer(200u);
            using NativeFacadeSet? clientB = TryCreateNativePeer(300u);
            if (server is null || clientA is null || clientB is null)
            {
                return new RuntimeTransportSummary(false, false, 0, 0);
            }

            _ = server.Net.Pump();
            _ = clientA.Net.Pump();
            _ = clientB.Net.Pump();

            server.Net.Send(200u, NetworkChannel.Unreliable, [1, 2, 3, 4]);
            server.Net.Send(300u, NetworkChannel.Unreliable, [5, 6, 7, 8]);
            clientA.Net.Send(100u, NetworkChannel.ReliableOrdered, [11, 12]);
            clientB.Net.Send(100u, NetworkChannel.ReliableOrdered, [13, 14, 15]);

            int serverMessages = CountMessageEvents(server.Net.Pump());
            int clientMessages = CountMessageEvents(clientA.Net.Pump()) + CountMessageEvents(clientB.Net.Pump());
            bool success = serverMessages >= 2 && clientMessages >= 2;
            return new RuntimeTransportSummary(true, success, serverMessages, clientMessages);
        }
        catch (Exception ex) when (
            ex is DllNotFoundException or
            EntryPointNotFoundException or
            BadImageFormatException or
            FileNotFoundException or
            InvalidDataException or
            InvalidOperationException or
            TypeInitializationException)
        {
            return new RuntimeTransportSummary(false, false, 0, 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DFF_NET_LOCAL_PEER_ID", previousPeerId);
        }
    }

    private static NativeFacadeSet? TryCreateNativePeer(uint peerId)
    {
        Environment.SetEnvironmentVariable("DFF_NET_LOCAL_PEER_ID", peerId.ToString(CultureInfo.InvariantCulture));
        return NativeFacadeFactory.CreateNativeFacadeSet();
    }

    private static int CountMessageEvents(IReadOnlyList<NetEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        int count = 0;
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Kind == NetEventKind.Message)
            {
                count++;
            }
        }

        return count;
    }

    private static InMemoryNetSession CreateSession(int tickRate)
    {
        var config = new NetworkConfig(
            TickRateHz: tickRate,
            MaxPayloadBytes: 4096,
            MaxRpcPerTickPerClient: 32,
            MaxEntitiesPerSnapshot: 8192,
            SimulatedRttMs: 42.0,
            SimulatedPacketLossPercent: 1.5);
        return new InMemoryNetSession(config);
    }

    private static void UpsertProceduralEntities(
        InMemoryNetSession session,
        IReadOnlyList<LevelMeshChunk> chunks,
        uint proceduralSeed,
        uint firstClientId,
        uint secondClientId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(chunks);

        for (int i = 0; i < chunks.Count; i++)
        {
            LevelMeshChunk chunk = chunks[i];
            NetProceduralRecipeRef recipe = BuildRecipe(chunk);
            string assetKey = BuildAssetKey(chunk, recipe);
            byte[] payload = BuildTransformPayload(chunk, i);
            uint ownerClientId = (i & 1) == 0 ? firstClientId : secondClientId;
            var entity = new NetEntityState(
                entityId: checked((uint)(i + 1)),
                ownerClientId: ownerClientId,
                proceduralSeed: proceduralSeed,
                assetKey: assetKey,
                components: [new NetComponentState(ComponentId, payload)],
                proceduralRecipe: recipe);
            session.UpsertServerEntity(entity);
        }
    }

    private static NetProceduralRecipeRef BuildRecipe(LevelMeshChunk chunk)
    {
        LevelChunkTag tag = LevelChunkTag.Parse(chunk.MeshTag);
        int surfaceSizeBias = tag.NodeType is LevelNodeType.Room or LevelNodeType.Junction ? 16 : 0;
        int surfaceWidth = DefaultSurfaceWidth + surfaceSizeBias;
        int surfaceHeight = DefaultSurfaceHeight + surfaceSizeBias;
        string recipeHash = ComputeRecipeHash(chunk, surfaceWidth, surfaceHeight);

        return new NetProceduralRecipeRef(
            generatorId: "proc/chunk/content",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: recipeHash,
            parameters: new Dictionary<string, string>
            {
                ["meshTag"] = chunk.MeshTag,
                ["nodeId"] = chunk.NodeId.ToString(CultureInfo.InvariantCulture),
                ["surfaceWidth"] = surfaceWidth.ToString(CultureInfo.InvariantCulture),
                ["surfaceHeight"] = surfaceHeight.ToString(CultureInfo.InvariantCulture)
            });
    }

    private static string BuildAssetKey(LevelMeshChunk chunk, NetProceduralRecipeRef recipe)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"proc/chunk/{chunk.NodeId}/{recipe.RecipeHash}");
    }

    private static string ComputeRecipeHash(LevelMeshChunk chunk, int surfaceWidth, int surfaceHeight)
    {
        string payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{chunk.MeshTag}|{chunk.NodeId}|{surfaceWidth}|{surfaceHeight}");
        uint hash = 2166136261u;
        foreach (char ch in payload)
        {
            hash ^= ch;
            hash *= 16777619u;
        }

        return hash.ToString("X8", CultureInfo.InvariantCulture);
    }

    private static byte[] BuildTransformPayload(LevelMeshChunk chunk, int index)
    {
        var bytes = new byte[8];
        WriteInt32LittleEndian(bytes, 0, chunk.NodeId);
        WriteInt32LittleEndian(bytes, 4, index);
        return bytes;
    }

    private static void WriteInt32LittleEndian(byte[] destination, int offset, int value)
    {
        destination[offset] = (byte)(value & 0xFF);
        destination[offset + 1] = (byte)((value >> 8) & 0xFF);
        destination[offset + 2] = (byte)((value >> 16) & 0xFF);
        destination[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static int ResolveTickRate(double fixedDeltaSeconds)
    {
        int tickRate = checked((int)Math.Round(1.0 / fixedDeltaSeconds, MidpointRounding.AwayFromZero));
        return Math.Clamp(tickRate, 10, 240);
    }

    private static bool AreProceduralSnapshotsEquivalent(
        IReadOnlyList<NetProceduralChunkEntityBinding> first,
        IReadOnlyList<NetProceduralChunkEntityBinding> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Count; i++)
        {
            NetProceduralChunkEntityBinding left = first[i];
            NetProceduralChunkEntityBinding right = second[i];
            if (left.EntityId != right.EntityId ||
                !string.Equals(left.AssetKey, right.AssetKey, StringComparison.Ordinal) ||
                left.ProceduralSeed != right.ProceduralSeed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreNetSnapshotsEquivalent(NetSnapshot first, NetSnapshot second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Tick != second.Tick || first.Entities.Count != second.Entities.Count)
        {
            return false;
        }

        for (int i = 0; i < first.Entities.Count; i++)
        {
            NetEntityState left = first.Entities[i];
            NetEntityState right = second.Entities[i];
            if (left.EntityId != right.EntityId ||
                left.OwnerClientId != right.OwnerClientId ||
                left.ProceduralSeed != right.ProceduralSeed ||
                !string.Equals(left.AssetKey, right.AssetKey, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<MultiplayerDemoOwnershipStats> BuildOwnershipStats(
        NetSnapshot snapshot,
        IReadOnlyList<uint> clientIds)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(clientIds);

        var countsByClientId = new SortedDictionary<uint, int>();
        foreach (uint clientId in clientIds)
        {
            countsByClientId[clientId] = 0;
        }

        foreach (NetEntityState entity in snapshot.Entities)
        {
            if (entity.OwnerClientId is not uint ownerClientId || !countsByClientId.ContainsKey(ownerClientId))
            {
                continue;
            }

            countsByClientId[ownerClientId] = checked(countsByClientId[ownerClientId] + 1);
        }

        return countsByClientId
            .Select(static pair => new MultiplayerDemoOwnershipStats(pair.Key, pair.Value))
            .ToArray();
    }

    private static MultiplayerDemoStats ToStats(NetPeerStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return new MultiplayerDemoStats(
            BytesSent: stats.BytesSent,
            BytesReceived: stats.BytesReceived,
            MessagesSent: stats.MessagesSent,
            MessagesReceived: stats.MessagesReceived,
            MessagesDropped: stats.MessagesDropped,
            RoundTripTimeMs: stats.RoundTripTimeMs,
            LossPercent: stats.LossPercent,
            AverageSendBandwidthKbps: stats.AverageSendBandwidthKbps,
            AverageReceiveBandwidthKbps: stats.AverageReceiveBandwidthKbps,
            PeakSendBandwidthKbps: stats.PeakSendBandwidthKbps,
            PeakReceiveBandwidthKbps: stats.PeakReceiveBandwidthKbps);
    }

    private static void WriteProfileLog(string fullPath, MultiplayerDemoSummary summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentNullException.ThrowIfNull(summary);

        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(fullPath, BuildProfileLogLines(summary));
    }

    private static IReadOnlyList<string> BuildProfileLogLines(MultiplayerDemoSummary summary)
    {
        var lines = new List<string>(summary.ClientStats.Count + 3)
        {
            string.Format(
                CultureInfo.InvariantCulture,
                "seed={0} proceduralSeed={1} fixedDt={2:F6} tickRateHz={3} simulatedTicks={4} synchronized={5} runtimeTransportEnabled={6} runtimeTransportSucceeded={7}",
                summary.Seed,
                summary.ProceduralSeed,
                summary.FixedDeltaSeconds,
                summary.TickRateHz,
                summary.SimulatedTicks,
                summary.Synchronized,
                summary.RuntimeTransport.Enabled,
                summary.RuntimeTransport.Succeeded),
            string.Format(
                CultureInfo.InvariantCulture,
                "runtime-transport serverMessages={0} clientMessages={1}",
                summary.RuntimeTransport.ServerMessagesReceived,
                summary.RuntimeTransport.ClientMessagesReceived),
            FormatStatsLine("server", summary.ServerStats)
        };

        foreach (MultiplayerDemoClientStats client in summary.ClientStats.OrderBy(static x => x.ClientId))
        {
            lines.Add(FormatStatsLine(
                string.Format(CultureInfo.InvariantCulture, "client-{0}", client.ClientId),
                client.Stats));
        }

        return lines;
    }

    private static string FormatStatsLine(string scope, MultiplayerDemoStats stats)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(stats);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} bytesSent={1} bytesReceived={2} messagesSent={3} messagesReceived={4} dropped={5} rttMs={6:F3} lossPercent={7:F3} sendKbps={8:F3} receiveKbps={9:F3} peakSendKbps={10:F3} peakReceiveKbps={11:F3}",
            scope,
            stats.BytesSent,
            stats.BytesReceived,
            stats.MessagesSent,
            stats.MessagesReceived,
            stats.MessagesDropped,
            stats.RoundTripTimeMs,
            stats.LossPercent,
            stats.AverageSendBandwidthKbps,
            stats.AverageReceiveBandwidthKbps,
            stats.PeakSendBandwidthKbps,
            stats.PeakReceiveBandwidthKbps);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
