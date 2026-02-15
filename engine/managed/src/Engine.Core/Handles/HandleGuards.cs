using System;

namespace Engine.Core.Handles;

internal static class HandleGuards
{
    public static void RequireNonZero(string parameterName, uint value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Handle value must be non-zero.");
        }
    }
}
