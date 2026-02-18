using System;
using System.Collections.Generic;
using Engine.Core.Handles;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;

namespace Engine.UI;

public sealed partial class RetainedUiFacade : IUiFacade
{
    private readonly Queue<UiQueuedInteraction> _queuedInteractions = [];
    private readonly UiDocument _document;
    private EntityId _renderBatchEntity = EntityId.Invalid;
    private UiInputField? _focusedInput;
    private UiElement? _hoveredElement;

    public RetainedUiFacade()
        : this(new UiDocument())
    {
    }

    public RetainedUiFacade(UiDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
    }

    public UiDocument Document => _document;

    public string DumpTree()
    {
        return DumpTree(includeResolvedStyles: false);
    }

    public string DumpTree(bool includeResolvedStyles)
    {
        UiLayoutEngine.Apply(_document);
        UpdateScrollViewMeasurements();
        return UiTreeDumper.Dump(_document, includeResolvedStyles);
    }

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

    public void QueuePointerDown(float x, float y)
    {
        if (!float.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pointer X coordinate must be finite.");
        }

        if (!float.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Pointer Y coordinate must be finite.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreatePointerDown(x, y));
    }

    public void QueuePointerUp(float x, float y)
    {
        if (!float.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pointer X coordinate must be finite.");
        }

        if (!float.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Pointer Y coordinate must be finite.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreatePointerUp(x, y));
    }

    public void QueueScroll(float x, float y, float wheelDelta)
    {
        if (!float.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pointer X coordinate must be finite.");
        }

        if (!float.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Pointer Y coordinate must be finite.");
        }

        if (!float.IsFinite(wheelDelta))
        {
            throw new ArgumentOutOfRangeException(nameof(wheelDelta), "Wheel delta must be finite.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreatePointerScroll(x, y, wheelDelta));
    }

    public void QueuePointerMove(float x, float y)
    {
        if (!float.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pointer X coordinate must be finite.");
        }

        if (!float.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Pointer Y coordinate must be finite.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreatePointerMove(x, y));
    }

    public void QueueTextInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return;
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreateTextInput(text));
    }

    public void QueueKeyDown(UiKey key)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported key value.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreateKeyDown(key));
    }

    public void QueueKeyUp(UiKey key)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported key value.");
        }

        _queuedInteractions.Enqueue(UiQueuedInteraction.CreateKeyUp(key));
    }

    public void QueueBackspace()
    {
        _queuedInteractions.Enqueue(UiQueuedInteraction.CreateBackspace());
    }

    public void Update(World world, in FrameTiming timing)
    {
        ArgumentNullException.ThrowIfNull(world);

        UiLayoutEngine.Apply(_document);
        UpdateScrollViewMeasurements();
        DispatchQueuedInteractions();
        IReadOnlyList<UiDrawCommand> commands = BuildCommands();
        WriteRenderBatch(world, commands);
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
}
