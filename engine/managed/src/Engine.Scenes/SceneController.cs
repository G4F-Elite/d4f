using Engine.ECS;

namespace Engine.Scenes;

public sealed class SceneController
{
    private readonly World _world;
    private Scene? _currentScene;

    public SceneController(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public Scene? CurrentScene => _currentScene;

    public void Load(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (ReferenceEquals(_currentScene, scene))
        {
            return;
        }

        _currentScene?.OnDestroy(_world);
        _currentScene = scene;
        _currentScene.OnCreate(_world);
    }

    public void Load<TScene>() where TScene : Scene, new()
    {
        Load(new TScene());
    }

    public void Unload()
    {
        if (_currentScene is null)
        {
            return;
        }

        _currentScene.OnDestroy(_world);
        _currentScene = null;
    }
}
