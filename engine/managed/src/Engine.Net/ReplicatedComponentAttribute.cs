namespace Engine.Net;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ReplicatedComponentAttribute : Attribute
{
    public ReplicatedComponentAttribute(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            throw new ArgumentException("Component id cannot be empty.", nameof(componentId));
        }

        ComponentId = componentId.Trim();
    }

    public string ComponentId { get; }
}
