using System.Reflection;
using System.Reflection.Emit;
using Engine.Content;

namespace Engine.Tests.Content;

public sealed class AssetRegistryDiscoveryTests
{
    [Fact]
    public void BuildFromAssemblies_ShouldCollectAttributedAssets()
    {
        Assembly first = BuildAssemblyWithSingleAsset("Discovery/One");
        Assembly second = BuildAssemblyWithSingleAsset("Discovery/Two");

        AssetRegistry registry = AssetRegistryDiscovery.BuildFromAssemblies([first, second]);

        Assert.True(registry.TryGet("Discovery/One", out _));
        Assert.True(registry.TryGet("Discovery/Two", out _));
    }

    [Fact]
    public void BuildFromLoadedAssemblies_ShouldHonorFilter()
    {
        string assemblyName = $"Engine.Tests.Content.Dynamic.Discovery.{Guid.NewGuid():N}";
        Assembly dynamicAssembly = BuildAssemblyWithSingleAsset("Discovery/Filtered", assemblyName);

        AssetRegistry registry = AssetRegistryDiscovery.BuildFromLoadedAssemblies(
            asm => string.Equals(asm.FullName, dynamicAssembly.FullName, StringComparison.Ordinal));

        Assert.True(registry.TryGet("Discovery/Filtered", out _));
        Assert.Single(registry.Entries);
    }

    [Fact]
    public void InMemoryAssetsProvider_CtorWithAssemblies_ShouldUseDiscoveredRegistry()
    {
        (Assembly assetsAssembly, Type assetType) = BuildAssemblyWithSingleAssetAndType("Discovery/ProviderAsset");
        var provider = new InMemoryAssetsProvider([assetsAssembly], buildConfigHash: "CFG");
        object instance = Activator.CreateInstance(assetType)
            ?? throw new InvalidOperationException("Could not create dynamic asset instance.");
        typeof(InMemoryAssetsProvider)
            .GetMethod(nameof(InMemoryAssetsProvider.RegisterPathAsset))
            ?.MakeGenericMethod(assetType)
            .Invoke(provider, ["Discovery/ProviderAsset", instance]);
        object loaded = typeof(InMemoryAssetsProvider)
            .GetMethod(nameof(InMemoryAssetsProvider.Load))
            ?.MakeGenericMethod(assetType)
            .Invoke(provider, ["Discovery/ProviderAsset"])
            ?? throw new InvalidOperationException("Could not load dynamic asset instance.");

        Assert.Same(instance, loaded);
    }

    private static Assembly BuildAssemblyWithSingleAsset(string path, string? explicitAssemblyName = null)
    {
        return BuildAssemblyWithSingleAsset(path, out _, explicitAssemblyName);
    }

    private static (Assembly Assembly, Type AssetType) BuildAssemblyWithSingleAssetAndType(string path)
    {
        Assembly assembly = BuildAssemblyWithSingleAsset(path, out Type assetType, explicitAssemblyName: null);
        return (assembly, assetType);
    }

    private static Assembly BuildAssemblyWithSingleAsset(
        string path,
        out Type assetType,
        string? explicitAssemblyName)
    {
        string assemblyName = explicitAssemblyName ?? $"Engine.Tests.Content.Dynamic.Discovery.{Guid.NewGuid():N}";
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");

        TypeBuilder asset = moduleBuilder.DefineType(
            $"DynamicAsset_{Guid.NewGuid():N}",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        asset.SetCustomAttribute(CreateAssetAttribute(path));
        assetType = asset.CreateType()
            ?? throw new InvalidOperationException("Failed to build dynamic asset type.");
        return assemblyBuilder;
    }

    private static CustomAttributeBuilder CreateAssetAttribute(string path)
    {
        ConstructorInfo ctor = typeof(DffAssetAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("DffAssetAttribute constructor not found.");
        return new CustomAttributeBuilder(ctor, [path]);
    }
}
