using System;
using System.Numerics;
using Engine.Core.Handles;
using Engine.Rendering;
using Xunit;

namespace Engine.Tests.Rendering;

public sealed class RenderMeshInstanceTests
{
    [Fact]
    public void LegacyConstructor_UsesIdentityWorldAndDerivedSortKeys()
    {
        var instance = new RenderMeshInstance(
            new MeshHandle(10),
            new MaterialHandle(20),
            new TextureHandle(30));

        Assert.Equal(Matrix4x4.Identity, instance.WorldMatrix);
        Assert.Equal((uint)20, instance.SortKeyHigh);
        Assert.Equal((uint)10, instance.SortKeyLow);
    }

    [Fact]
    public void ExtendedConstructor_StoresProvidedWorldAndSortKeys()
    {
        var world = new Matrix4x4(
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16);

        var instance = new RenderMeshInstance(
            new MeshHandle(11),
            new MaterialHandle(22),
            new TextureHandle(33),
            world,
            100,
            200);

        Assert.Equal(world, instance.WorldMatrix);
        Assert.Equal((uint)100, instance.SortKeyHigh);
        Assert.Equal((uint)200, instance.SortKeyLow);
    }

    [Fact]
    public void Constructors_ValidateHandles()
    {
        Assert.Throws<ArgumentException>(() => new RenderMeshInstance(MeshHandle.Invalid, new MaterialHandle(1), new TextureHandle(1)));
        Assert.Throws<ArgumentException>(() => new RenderMeshInstance(new MeshHandle(1), MaterialHandle.Invalid, new TextureHandle(1)));
        Assert.Throws<ArgumentException>(() => new RenderMeshInstance(new MeshHandle(1), new MaterialHandle(1), TextureHandle.Invalid));
    }
}
