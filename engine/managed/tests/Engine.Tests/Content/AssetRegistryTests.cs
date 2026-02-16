using System.Reflection;
using System.Reflection.Emit;
using Engine.Content;

namespace Engine.Tests.Content;

public sealed class AssetRegistryTests
{
    [Fact]
    public void Register_ShouldAddDescriptor_WithNormalizedPathAndTags()
    {
        var registry = new AssetRegistry();

        registry.Register(typeof(UiButtonStyleAsset));

        AssetDescriptor descriptor = registry.GetRequired("UI/Buttons/Main");
        Assert.Equal(typeof(UiButtonStyleAsset), descriptor.AssetType);
        Assert.Equal("ui", descriptor.Category);
        Assert.Equal(["controls", "primary"], descriptor.Tags);
    }

    [Fact]
    public void RegisterAssembly_ShouldCollectAttributedTypes()
    {
        var registry = new AssetRegistry();
        Assembly dynamicAssembly = BuildAssemblyWithAssets();

        registry.RegisterAssembly(dynamicAssembly);

        Assert.True(registry.TryGet("Dynamic/One", out _));
        Assert.True(registry.TryGet("Dynamic/Two", out _));
    }

    [Fact]
    public void Register_ShouldFail_WhenTypeHasNoAttribute()
    {
        var registry = new AssetRegistry();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => registry.Register(typeof(NoAssetAttributeType)));
        Assert.Contains("missing [DffAssetAttribute]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_ShouldFail_WhenPathIsDuplicatedByDifferentType()
    {
        var registry = new AssetRegistry();
        registry.Register(typeof(UiButtonStyleAsset));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => registry.Register(typeof(UiButtonStyleAssetDuplicate)));
        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Register_ShouldFail_WhenPathContainsRelativeSegments()
    {
        var registry = new AssetRegistry();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => registry.Register(typeof(InvalidPathAsset)));
        Assert.Contains("relative navigation segments", exception.Message, StringComparison.Ordinal);
    }

    [DffAsset("UI\\Buttons\\Main", Category = "ui", Tags = ["primary", "controls", "primary"])]
    private sealed class UiButtonStyleAsset;

    [DffAsset("UI/Buttons/Secondary", Category = "ui", Tags = ["secondary"])]
    private sealed class UiSecondaryButtonStyleAsset;

    [DffAsset("UI/Buttons/Main", Category = "ui", Tags = ["duplicate"])]
    private sealed class UiButtonStyleAssetDuplicate;

    [DffAsset("../invalid/path", Category = "ui", Tags = ["invalid"])]
    private sealed class InvalidPathAsset;

    private sealed class NoAssetAttributeType;

    private static Assembly BuildAssemblyWithAssets()
    {
        string assemblyName = $"Engine.Tests.Content.DynamicAssets.{Guid.NewGuid():N}";
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");

        TypeBuilder firstAsset = moduleBuilder.DefineType(
            "DynamicAssetOne",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        firstAsset.SetCustomAttribute(CreateAssetAttribute("Dynamic/One"));
        _ = firstAsset.CreateType();

        TypeBuilder secondAsset = moduleBuilder.DefineType(
            "DynamicAssetTwo",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        secondAsset.SetCustomAttribute(CreateAssetAttribute("Dynamic/Two"));
        _ = secondAsset.CreateType();

        return assemblyBuilder;
    }

    private static CustomAttributeBuilder CreateAssetAttribute(string path)
    {
        ConstructorInfo ctor = typeof(DffAssetAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("DffAssetAttribute constructor not found.");
        return new CustomAttributeBuilder(ctor, [path]);
    }
}
