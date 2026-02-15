using System;
using Engine.Core.Handles;
using Xunit;

namespace Engine.Tests.Handles;

public sealed class HandleValueTypeTests
{
    [Fact]
    public void HandleConstructors_RejectZeroValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextureHandle(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeshHandle(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaterialHandle(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BodyHandle(0));
    }

    [Fact]
    public void DefaultHandles_AreInvalid()
    {
        Assert.False(default(TextureHandle).IsValid);
        Assert.False(default(MeshHandle).IsValid);
        Assert.False(default(MaterialHandle).IsValid);
        Assert.False(default(BodyHandle).IsValid);
        Assert.False(default(EntityId).IsValid);
    }

    [Fact]
    public void EntityIdConstructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityId(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EntityId(0, 0));

        var entityId = new EntityId(4, 2);
        Assert.True(entityId.IsValid);
        Assert.Equal(4, entityId.Index);
        Assert.Equal(2u, entityId.Generation);
    }
}
