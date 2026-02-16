using System;
using System.Numerics;

namespace Engine.Physics;

public struct CharacterController
{
    public CharacterController(
        float radius,
        float height,
        float skinWidth,
        Vector3 desiredVelocity,
        bool isGrounded = false)
    {
        if (!float.IsFinite(radius) || radius <= 0.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Controller radius must be finite and positive.");
        }

        if (!float.IsFinite(height) || height <= radius * 2.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Controller height must be finite and greater than diameter.");
        }

        if (!float.IsFinite(skinWidth) || skinWidth < 0.0f || skinWidth >= radius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(skinWidth),
                "Skin width must be finite, non-negative, and smaller than controller radius.");
        }

        Radius = radius;
        Height = height;
        SkinWidth = skinWidth;
        DesiredVelocity = desiredVelocity;
        IsGrounded = isGrounded;
    }

    public float Radius;

    public float Height;

    public float SkinWidth;

    public Vector3 DesiredVelocity;

    public bool IsGrounded;
}
