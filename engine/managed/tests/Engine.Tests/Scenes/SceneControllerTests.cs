using Engine.ECS;
using Engine.Scenes;
using Xunit;

namespace Engine.Tests.Scenes;

public sealed class SceneControllerTests
{
    [Fact]
    public void Load_CallsCreateAndTracksCurrentScene()
    {
        var world = new World();
        var controller = new SceneController(world);
        var scene = new RecordingScene();

        controller.Load(scene);

        Assert.Same(scene, controller.CurrentScene);
        Assert.Equal(1, scene.CreateCalls);
        Assert.Equal(0, scene.DestroyCalls);
    }

    [Fact]
    public void Load_NewScene_DestroysPreviousAndCreatesNew()
    {
        var world = new World();
        var controller = new SceneController(world);
        var first = new RecordingScene();
        var second = new RecordingScene();

        controller.Load(first);
        controller.Load(second);

        Assert.Same(second, controller.CurrentScene);
        Assert.Equal(1, first.CreateCalls);
        Assert.Equal(1, first.DestroyCalls);
        Assert.Equal(1, second.CreateCalls);
        Assert.Equal(0, second.DestroyCalls);
    }

    [Fact]
    public void Load_SameScene_IsNoOp()
    {
        var world = new World();
        var controller = new SceneController(world);
        var scene = new RecordingScene();

        controller.Load(scene);
        controller.Load(scene);

        Assert.Equal(1, scene.CreateCalls);
        Assert.Equal(0, scene.DestroyCalls);
    }

    [Fact]
    public void Unload_DestroysCurrentScene()
    {
        var world = new World();
        var controller = new SceneController(world);
        var scene = new RecordingScene();
        controller.Load(scene);

        controller.Unload();

        Assert.Null(controller.CurrentScene);
        Assert.Equal(1, scene.DestroyCalls);
    }

    [Fact]
    public void ConstructorAndLoad_RejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SceneController(null!));

        var controller = new SceneController(new World());
        Assert.Throws<ArgumentNullException>(() => controller.Load(null!));
    }

    [Fact]
    public void LoadGeneric_CreatesAndLoadsSceneInstance()
    {
        var controller = new SceneController(new World());

        controller.Load<GenericScene>();

        Assert.IsType<GenericScene>(controller.CurrentScene);
    }

    private sealed class RecordingScene : Scene
    {
        public int CreateCalls { get; private set; }

        public int DestroyCalls { get; private set; }

        public override void OnCreate(World world)
        {
            CreateCalls++;
        }

        public override void OnDestroy(World world)
        {
            DestroyCalls++;
        }
    }

    private sealed class GenericScene : Scene
    {
        public override void OnCreate(World world)
        {
        }
    }
}
