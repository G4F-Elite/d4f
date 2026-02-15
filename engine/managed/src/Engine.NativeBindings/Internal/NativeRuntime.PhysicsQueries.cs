using System;
using System.Buffers;
using System.Runtime.InteropServices;
using Engine.NativeBindings.Internal.Interop;
using Engine.Physics;

namespace Engine.NativeBindings.Internal;

internal sealed partial class NativeRuntime
{
    public bool Sweep(in PhysicsSweepQuery query, out PhysicsSweepHit hit)
    {
        ThrowIfDisposed();

        var nativeQuery = new EngineNativeSweepQuery
        {
            Origin0 = query.Origin.X,
            Origin1 = query.Origin.Y,
            Origin2 = query.Origin.Z,
            Direction0 = query.Direction.X,
            Direction1 = query.Direction.Y,
            Direction2 = query.Direction.Z,
            MaxDistance = query.MaxDistance,
            IncludeTriggers = query.IncludeTriggers ? (byte)1 : (byte)0,
            ShapeType = (byte)query.ShapeType,
            Reserved0 = 0,
            Reserved1 = 0,
            ShapeDimensions0 = query.ShapeDimensions.X,
            ShapeDimensions1 = query.ShapeDimensions.Y,
            ShapeDimensions2 = query.ShapeDimensions.Z
        };

        NativeStatusGuard.ThrowIfFailed(
            _interop.PhysicsSweep(_physics, in nativeQuery, out var nativeHit),
            "physics_sweep");

        if (nativeHit.HasHit == 0)
        {
            hit = default;
            return false;
        }

        hit = new PhysicsSweepHit(
            DecodeBodyHandle(nativeHit.Body),
            nativeHit.Distance,
            new(nativeHit.Point0, nativeHit.Point1, nativeHit.Point2),
            new(nativeHit.Normal0, nativeHit.Normal1, nativeHit.Normal2),
            nativeHit.IsTrigger != 0);
        return true;
    }

    public int Overlap(in PhysicsOverlapQuery query, Span<PhysicsOverlapHit> hits)
    {
        ThrowIfDisposed();

        var nativeQuery = new EngineNativeOverlapQuery
        {
            Center0 = query.Center.X,
            Center1 = query.Center.Y,
            Center2 = query.Center.Z,
            IncludeTriggers = query.IncludeTriggers ? (byte)1 : (byte)0,
            ShapeType = (byte)query.ShapeType,
            Reserved0 = 0,
            Reserved1 = 0,
            ShapeDimensions0 = query.ShapeDimensions.X,
            ShapeDimensions1 = query.ShapeDimensions.Y,
            ShapeDimensions2 = query.ShapeDimensions.Z
        };

        uint hitCapacity = checked((uint)hits.Length);
        EngineNativeOverlapHit[]? nativeHits = null;
        var pinnedHits = default(GCHandle);

        try
        {
            var nativeHitsPtr = IntPtr.Zero;
            if (hitCapacity > 0u)
            {
                nativeHits = ArrayPool<EngineNativeOverlapHit>.Shared.Rent(hits.Length);
                pinnedHits = GCHandle.Alloc(nativeHits, GCHandleType.Pinned);
                nativeHitsPtr = pinnedHits.AddrOfPinnedObject();
            }

            NativeStatusGuard.ThrowIfFailed(
                _interop.PhysicsOverlap(_physics, in nativeQuery, nativeHitsPtr, hitCapacity, out var nativeHitCount),
                "physics_overlap");

            if (nativeHitCount > hitCapacity)
            {
                throw new InvalidOperationException(
                    $"Native physics returned overlap hit count {nativeHitCount}, exceeding capacity {hitCapacity}.");
            }

            int hitCount = checked((int)nativeHitCount);
            for (int i = 0; i < hitCount; i++)
            {
                var nativeHit = nativeHits![i];
                hits[i] = new PhysicsOverlapHit(DecodeBodyHandle(nativeHit.Body), nativeHit.IsTrigger != 0);
            }

            return hitCount;
        }
        finally
        {
            if (pinnedHits.IsAllocated)
            {
                pinnedHits.Free();
            }

            if (nativeHits is not null)
            {
                ArrayPool<EngineNativeOverlapHit>.Shared.Return(nativeHits, clearArray: true);
            }
        }
    }
}
