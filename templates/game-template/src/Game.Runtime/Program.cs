using System.Globalization;
using System.Numerics;
using Engine.Audio;
using Engine.Content;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.NativeBindings;
using Engine.Net;
using Engine.Procedural;
using Engine.UI;

const uint DefaultSeed = 1337u;
uint seed = ParseSeed(args);
TryConfigurePackagedContent();

LevelGenResult level = LevelGenerator.Generate(new LevelGenOptions(
    Seed: seed,
    TargetNodes: 24,
    Density: 0.72f,
    Danger: 0.48f,
    Complexity: 0.68f));

ProcMeshData mesh = BuildDemoMesh();
ProceduralTextureSurface surface = TextureBuilder.GenerateSurfaceMaps(new ProceduralTextureRecipe(
    Kind: ProceduralTextureKind.Perlin,
    Width: 96,
    Height: 96,
    Seed: seed ^ 0xA341_316Cu,
    FbmOctaves: 4,
    Frequency: 6f));
ProceduralLitMaterialBundle materialBundle = ProceduralMaterialFactory.CreateLitPbrFromSurface(
    surface,
    textureKeyPrefix: "demo/rock",
    roughness: 0.62f,
    metallic: 0.08f);

byte[] soundBlob = ProceduralSoundBlobBuilder.BuildMonoPcmBlob(
    new ProceduralSoundRecipe(
        Oscillator: OscillatorType.Triangle,
        FrequencyHz: 220f,
        Gain: 0.30f,
        SampleRate: 44100,
        Seed: seed ^ 0x9E37_79B9u,
        Envelope: new AdsrEnvelope(0.02f, 0.12f, 0.5f, 0.2f),
        Lfo: new LfoSettings(4.0f, 0.08f),
        Filter: new OnePoleLowPassFilter(4200f)),
    durationSeconds: 1.6f,
    loop: false);

UiSummary ui = BuildUiPreviewSummary(level);
NetSummary net = RunMultiplayerSummary(level, seed);

Console.WriteLine($"__GAME_NAME__ runtime demo (seed={seed}):");
Console.WriteLine($"- Level: nodes={level.Graph.Nodes.Count}, chunks={level.MeshChunks.Count}, spawns={level.SpawnPoints.Count}");
Console.WriteLine($"- Mesh: vertices={mesh.Vertices.Count}, triangles={mesh.Indices.Count / 3}, lods={mesh.Lods.Count}");
Console.WriteLine($"- Material: template={materialBundle.Material.Template}, textures={materialBundle.Textures.Count}");
Console.WriteLine($"- Audio: blob-bytes={soundBlob.Length}");
Console.WriteLine($"- UI: draw-commands={ui.DrawCommandCount}, visible-list-items={ui.VisibleItemCount}");
Console.WriteLine($"- Net: tick={net.Tick}, clients={net.ClientCount}, replicated-entities={net.ReplicatedEntityCount}, server-rtt={net.ServerRttMs:F2}ms");

return;

static uint ParseSeed(string[] argv)
{
    for (int i = 0; i < argv.Length - 1; i++)
    {
        if (!string.Equals(argv[i], "--seed", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (uint.TryParse(argv[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
        {
            return parsed;
        }
    }

    return DefaultSeed;
}

static ProcMeshData BuildDemoMesh()
{
    MeshBuilder builder = new();
    builder.BeginSubmesh("demo-floor");

    int v0 = builder.AddVertex(new Vector3(-2f, 0f, -2f), Vector3.UnitY, Vector2.Zero);
    int v1 = builder.AddVertex(new Vector3(2f, 0f, -2f), Vector3.UnitY, Vector2.Zero);
    int v2 = builder.AddVertex(new Vector3(-2f, 0f, 2f), Vector3.UnitY, Vector2.Zero);
    int v3 = builder.AddVertex(new Vector3(2f, 0f, 2f), Vector3.UnitY, Vector2.Zero);

    builder.AddTriangle(v0, v1, v2);
    builder.AddTriangle(v1, v3, v2);
    builder.EndSubmesh();

    builder.GenerateUv(UvProjection.Box, scale: 0.5f);
    builder.GenerateLod(screenCoverage: 0.55f);
    return builder.Build();
}

static void TryConfigurePackagedContent()
{
    string runtimeConfigPath = PackagedRuntimeContentBootstrap.GetDefaultRuntimeConfigPath();
    if (!File.Exists(runtimeConfigPath))
    {
        return;
    }

    PackagedRuntimeContentBootstrap.ConfigureFromRuntimeConfig(
        NativeFacadeFactory.CreateContentRuntimeFacade(),
        runtimeConfigPath);
}

static UiSummary BuildUiPreviewSummary(LevelGenResult level)
{
    TextureHandle panelTexture = new(1u);
    TextureHandle buttonTexture = new(2u);
    TextureHandle fontTexture = new(3u);
    TextureHandle listItemTexture = new(4u);
    TextureHandle imageTexture = new(5u);
    TextureHandle compactListItemTexture = new(6u);

    UiDocument document = new();
    UiPanel root = new("root", panelTexture)
    {
        Width = 960f,
        Height = 540f,
        Padding = new UiThickness(18f, 18f, 18f, 18f),
        LayoutMode = UiLayoutMode.VerticalStack,
        LayoutGap = 10f
    };

    root.AddChild(new UiText("title", fontTexture, "__GAME_NAME__ procedural runtime"));
    root.AddChild(new UiImage("logo", imageTexture)
    {
        Width = 180f,
        Height = 48f,
        PreserveAspectRatio = true
    });
    root.AddChild(new UiButton("play", buttonTexture, "Start"));

    UiList compactList = new("spawn-categories", compactListItemTexture, fontTexture)
    {
        Width = 420f,
        Height = 72f,
        ItemHeight = 24f
    };
    compactList.SetItems(level.SpawnPoints.Select(static x => $"{x.Category} / node {x.NodeId}").ToArray());
    root.AddChild(compactList);

    UiVirtualizedList list = new("spawn-list", listItemTexture, fontTexture)
    {
        Width = 420f,
        Height = 180f,
        ItemHeight = 24f
    };
    list.SetItems(level.SpawnPoints.Select(static x => $"{x.Category} @ node {x.NodeId}").ToArray());
    root.AddChild(list);

    document.AddRoot(root);
    UiPreviewHost preview = new(document);
    IReadOnlyList<Engine.Rendering.UiDrawCommand> drawCommands = preview.BuildDrawData(
        new FrameTiming(0, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)));
    int visibleCount = Math.Min(
        list.Items.Count,
        Math.Max(1, (int)MathF.Ceiling(list.Height / list.ItemHeight) + 1));
    return new UiSummary(drawCommands.Count, visibleCount);
}

static NetSummary RunMultiplayerSummary(LevelGenResult level, uint seed)
{
    InMemoryNetSession session = new(new NetworkConfig(
        TickRateHz: 30,
        MaxPayloadBytes: 8192,
        MaxRpcPerTickPerClient: 16,
        MaxEntitiesPerSnapshot: 256,
        SimulatedRttMs: 28.0,
        SimulatedPacketLossPercent: 0.5));
    session.RegisterReplicatedComponent("transform");

    uint firstClientId = session.ConnectClient();
    uint secondClientId = session.ConnectClient();
    LevelMeshChunk firstChunk = level.MeshChunks[0];

    session.UpsertServerEntity(new NetEntityState(
        entityId: 1u,
        ownerClientId: null,
        proceduralSeed: seed,
        assetKey: firstChunk.MeshTag,
        components:
        [
            new NetComponentState("transform", [1, 2, 3, 4])
        ],
        proceduralRecipe: new NetProceduralRecipeRef(
            generatorId: "level.chunk",
            generatorVersion: 1,
            recipeVersion: 1,
            recipeHash: $"seed-{seed:X8}",
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["node"] = firstChunk.NodeId.ToString(CultureInfo.InvariantCulture),
                ["mesh"] = firstChunk.MeshTag
            })));

    long tick = session.Pump();
    NetSnapshot snapshot = session.GetClientSnapshot(firstClientId);
    NetPeerStats server = session.GetServerStats();
    _ = session.GetClientSnapshot(secondClientId);
    return new NetSummary(tick, session.ConnectedClientCount, snapshot.Entities.Count, server.RoundTripTimeMs);
}

internal readonly record struct UiSummary(int DrawCommandCount, int VisibleItemCount);

internal readonly record struct NetSummary(long Tick, int ClientCount, int ReplicatedEntityCount, double ServerRttMs);
