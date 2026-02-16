using System;
using System.Collections.Generic;
using Engine.Core.Geometry;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;

namespace Engine.UI;

public sealed class RetainedUiFacade : IUiFacade
{
    private readonly Queue<UiQueuedInteraction> _queuedInteractions = [];
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

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreateElementClick(elementId));
    }

    public void QueuePointerClick(float x, float y)
    {
        if (!float.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pointer X coordinate must be finite.");
        }

        if (!float.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Pointer Y coordinate must be finite.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreatePointerClick(x, y));
    }

    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);

        UiLayoutEngine.Apply(_document);
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
                AppendCommand(panel.BackgroundTexture, 1u, element.LayoutBounds, commands, ref vertexOffset, ref indexOffset);
                break;
            case UiButton button:
                AppendCommand(button.BackgroundTexture, 1u, element.LayoutBounds, commands, ref vertexOffset, ref indexOffset);
                break;
            case UiText text:
                uint glyphCount = checked((uint)text.Content.Length);
                if (glyphCount > 0u)
                {
                    AppendCommand(text.FontTexture, glyphCount, element.LayoutBounds, commands, ref vertexOffset, ref indexOffset);
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
        RectF bounds,
        List<UiDrawCommand> commands,
        ref uint vertexOffset,
        ref uint indexOffset)
    {
        uint vertexCount = checked(quadCount * 4u);
        uint indexCount = checked(quadCount * 6u);
        commands.Add(new UiDrawCommand(texture, vertexOffset, vertexCount, indexOffset, indexCount, bounds));
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
        while (_queuedInteractions.Count > 0)
        {
            UiQueuedInteraction interaction = _queuedInteractions.Dequeue();
            if (interaction.IsPointerClick)
            {
                DispatchPointerClick(interaction.PointerX, interaction.PointerY);
                continue;
            }

            DispatchClick(interaction.ElementId);
        }
    }

    private void DispatchClick(string elementId)
    {
        foreach (UiElement root in _document.Roots)
        {
            if (TryDispatchElementClick(root, elementId))
            {
                return;
            }
        }
    }

    private void DispatchPointerClick(float pointerX, float pointerY)
    {
        for (int i = _document.Roots.Count - 1; i >= 0; i--)
        {
            UiElement root = _document.Roots[i];
            if (TryDispatchPointerClick(root, pointerX, pointerY))
            {
                return;
            }
        }
    }

    private static bool TryDispatchElementClick(UiElement element, string elementId)
    {
        if (!element.Visible)
        {
            return false;
        }

        if (element.Id == elementId && element is UiButton button)
        {
            button.InvokeClick();
            return true;
        }

        foreach (UiElement child in element.Children)
        {
            if (TryDispatchElementClick(child, elementId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryDispatchPointerClick(UiElement element, float pointerX, float pointerY)
    {
        if (!element.Visible)
        {
            return false;
        }

        for (int i = element.Children.Count - 1; i >= 0; i--)
        {
            if (TryDispatchPointerClick(element.Children[i], pointerX, pointerY))
            {
                return true;
            }
        }

        if (!element.ContainsPoint(pointerX, pointerY))
        {
            return false;
        }

        if (element is UiButton button)
        {
            button.InvokeClick();
            return true;
        }

        return false;
    }

    private readonly record struct UiQueuedInteraction(string ElementId, float PointerX, float PointerY, bool IsPointerClick)
    {
        public static UiQueuedInteraction CreateElementClick(string elementId) =>
            new(elementId, 0.0f, 0.0f, false);

        public static UiQueuedInteraction CreatePointerClick(float x, float y) =>
            new(string.Empty, x, y, true);
    }
}
