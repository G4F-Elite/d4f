using System.Reflection;

namespace Engine.Content;

public static class AssetRegistryDiscovery
{
    public static AssetRegistry BuildFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var registry = new AssetRegistry();
        registry.RegisterAssemblies(assemblies);
        return registry;
    }

    public static AssetRegistry BuildFromLoadedAssemblies(Func<Assembly, bool>? filter = null)
    {
        Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        IEnumerable<Assembly> selected = filter is null
            ? loadedAssemblies
            : loadedAssemblies.Where(filter);
        return BuildFromAssemblies(selected);
    }
}
