using System;
using System.Collections.Generic;
using Engine.Core.Timing;
using Engine.ECS;
using Engine.Rendering;

namespace Engine.UI;

public sealed class UiPreviewHost
{
    private readonly World _world;
    private readonly RetainedUiFacade _facade;

    public UiPreviewHost()
        : this(new UiDocument())
    {
    }

    public UiPreviewHost(UiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _world = new World();
        _facade = new RetainedUiFacade(document);
    }

    public UiDocument Document => _facade.Document;

    public IReadOnlyList<UiDrawCommand> BuildDrawData(in FrameTiming timing)
    {
        _facade.Update(_world, timing);
        if (TryGetRenderBatch(out UiRenderBatch batch))
        {
            return batch.Commands;
        }

        return Array.Empty<UiDrawCommand>();
    }

    public string DumpTree() => _facade.DumpTree();

    public void QueueClick(string elementId) => _facade.QueueClick(elementId);

    public void QueuePointerClick(float x, float y) => _facade.QueuePointerClick(x, y);

    public void QueuePointerMove(float x, float y) => _facade.QueuePointerMove(x, y);

    public void QueueScroll(float x, float y, float wheelDelta) => _facade.QueueScroll(x, y, wheelDelta);

    public void QueueTextInput(string text) => _facade.QueueTextInput(text);

    public void QueueBackspace() => _facade.QueueBackspace();

    private bool TryGetRenderBatch(out UiRenderBatch renderBatch)
    {
        foreach ((_, UiRenderBatch batch) in _world.Query<UiRenderBatch>())
        {
            renderBatch = batch;
            return true;
        }

        renderBatch = default;
        return false;
    }
}
