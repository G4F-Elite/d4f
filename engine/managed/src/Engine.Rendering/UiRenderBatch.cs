using System;
using System.Collections.Generic;

namespace Engine.Rendering;

public readonly struct UiRenderBatch
{
    private readonly IReadOnlyList<UiDrawCommand>? _commands;

    public UiRenderBatch(IReadOnlyList<UiDrawCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands;
    }

    public IReadOnlyList<UiDrawCommand> Commands => _commands ?? Array.Empty<UiDrawCommand>();
}
