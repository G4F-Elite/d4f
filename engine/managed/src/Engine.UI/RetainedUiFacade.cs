using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;

namespace Engine.UI;

public sealed class RetainedUiFacade : IUiFacade
{
    private readonly Queue<string> _queuedClickIds = [];
    private readonly UiDocument _document;
    private EntityId _renderBatchEntity = EntityId.Invalid;

    public RetainedUiFacade()
        : this(new UiDocument())
    {
    }

    public RetainedUiFacade(UiDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public UiDocument Document => _document;

    public void QueueClick(string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException("Element id cannot be empty.", nameof(elementId));
        }

        _queuedClickIds.Enqueue(elementId);
    }

    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);

        DispatchQueuedClicks();
        IReadOnlyList<UiDrawCommand> commands = BuildCommands();
        WriteRenderBatch(world, commands);
    }

    private IReadOnlyList<UiDrawCommand> BuildCommands()
    {
        var commands = new List<UiDrawCommand>();
        uint vertexOffset = 0u;
        uint indexOffset = 0u;

        foreach (UiElement root in _document.Roots)
        {
            CollectCommands(root, commands, ref vertexOffset, ref indexOffset);
        }

        return commands.Count == 0
            ? Array.Empty<UiDrawCommand>()
            : commands.ToArray();
    }

    private static void CollectCommands(
        UiElement element,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        if (!element.Visible)
        {
            return;
        }

        switch (element)
        {
            case UiPanel panel:
                AppendCommand(panel.BackgroundTexture, 1u, commands, ref vertexOffset, ref indexOffset);
                break;
            case UiButton button:
                AppendCommand(button.BackgroundTexture, 1u, commands, ref vertexOffset, ref indexOffset);
                break;
            case UiText text:
                uint glyphCount = checked((uint)text.Content.Length);
                if (glyphCount > 0u)
                {
                    AppendCommand(text.FontTexture, glyphCount, commands, ref vertexOffset, ref indexOffset);
                }
                break;
        }

        foreach (UiElement child in element.Children)
        {
            CollectCommands(child, commands, ref vertexOffset, ref indexOffset);
        }
    }

    private static void AppendCommand(
        TextureHandle texture,
        uint quadCount,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        uint vertexCount = checked(quadCount * 4u);
        uint indexCount = checked(quadCount * 6u);
        commands.Add(new UiDrawCommand(texture, vertexOffset, vertexCount, indexOffset, indexCount));
        vertexOffset = checked(vertexOffset + vertexCount);
        indexOffset = checked(indexOffset + indexCount);
    }

    private void WriteRenderBatch(World world, IReadOnlyList<UiDrawCommand> commands)
    {
        EnsureRenderBatchEntity(world);
        world.SetComponent(_renderBatchEntity, new UiRenderBatch(commands));
    }

    private void EnsureRenderBatchEntity(World world)
    {
        if (_renderBatchEntity.IsValid && world.IsAlive(_renderBatchEntity))
        {
            return;
        }

        _renderBatchEntity = world.CreateEntity();
        world.AddComponent(_renderBatchEntity, new UiRenderBatch(Array.Empty<UiDrawCommand>()));
    }

    private void DispatchQueuedClicks()
    {
        while (_queuedClickIds.Count > 0)
        {
            string clickedId = _queuedClickIds.Dequeue();
            DispatchClick(clickedId);
        }
    }

    private void DispatchClick(string clickedId)
    {
        foreach (UiElement root in _document.Roots)
        {
            if (TryDispatchClick(root, clickedId))
            {
                return;
            }
        }
    }

    private static bool TryDispatchClick(UiElement element, string clickedId)
    {
        if (!element.Visible)
        {
            return false;
        }

        if (element.Id == clickedId && element is UiButton button)
        {
            button.InvokeClick();
            return true;
        }

        foreach (UiElement child in element.Children)
        {
            if (TryDispatchClick(child, clickedId))
            {
                return true;
            }
        }

        return false;
    }
}
