using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Numerics;
using zzio;
using zzio.scn;

namespace zzre.game;

public enum WaypointEdgeKind
{
    None,
    Walkable,
    Jumpable
}

public enum FindPathResult
{
    NotThereYet,
    Success,
    NotFound,
    Timeout
}

public class PathFinder : IDisposable
{
    public const uint InvalidId = uint.MaxValue;

    private readonly Random random = Random.Shared;
    private readonly WorldCollider collider;
    private readonly WaypointSystem wpSystem;
    private readonly FrozenDictionary<uint, int> idToIndex;
    private readonly bool[] isVisible;
    private bool disposedValue;

    public int WaypointCount => wpSystem.Waypoints.Length;

    public PathFinder(WaypointSystem wpSystem, WorldCollider collider)
    {
        this.collider = collider;
        this.wpSystem = wpSystem;
        idToIndex = wpSystem.Waypoints
            .Indexed()
            .ToFrozenDictionary(t => t.Value.Id, t => t.Index);

        isVisible = ArrayPool<bool>.Shared.Rent(WaypointCount * WaypointCount);
        if (WaypointCount > 0)
        {
            if (wpSystem.Waypoints[0].VisibleIds is null)
                RaycastVisibility();
            else
                SetVisibilityFromData();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                ArrayPool<bool>.Shared.Return(isVisible);
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void RaycastVisibility()
    {
        Array.Fill(isVisible, false);
        for (int i = 0; i < WaypointCount; i++)
        {
            isVisible[i * WaypointCount + i] = true;
            for (int j = i + 1; j < WaypointCount; j++)
            {
                if (collider.Intersects(new Line(wpSystem.Waypoints[i].Position, wpSystem.Waypoints[j].Position)))
                {
                    isVisible[i * WaypointCount + j] = true;
                    isVisible[j * WaypointCount + i] = true;
                }
            }
        }
    }

    private void SetVisibilityFromData()
    {
        Array.Fill(isVisible, false);
        for (int i = 0; i < WaypointCount; i++)
        {
            var visibleIds = wpSystem.Waypoints[i].VisibleIds;
            foreach (var id in visibleIds ?? [])
            {
                var j = idToIndex[id];
                isVisible[i * WaypointCount + j] = true;
            }
        }
    }

    public bool IsVisible(uint fromId, uint toId) => isVisible[fromId * WaypointCount + toId];

    public Vector3 this[uint id] => wpSystem.Waypoints[idToIndex[id]].Position;

    public interface IWaypointFilter
    {
        bool IsValid(in Waypoint waypoint) => true;
        bool IsValid(in Waypoint waypoint, int index) => IsValid(waypoint);
    }

    public uint NearestId<TFilter>(Vector3 position, TFilter filter)
        where TFilter : struct, IWaypointFilter
    {
        // TODO: Investigate acceleration structure for searching nearest waypoint
        float bestDistanceSqr = float.PositiveInfinity;
        uint bestId = InvalidId;
        for (int i = 0; i < WaypointCount; i++)
        {
            ref readonly var waypoint = ref wpSystem.Waypoints[i];
            if (!filter.IsValid(waypoint, i))
                continue;

            var curDistanceSqr = Vector3.DistanceSquared(waypoint.Position, position);
            if (curDistanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = curDistanceSqr;
                bestId = waypoint.Id;
            }
        }
        return bestId;
    }

    public uint FurthestId<TFilter>(Vector3 position, TFilter filter)
        where TFilter : IWaypointFilter
    {
        float bestDistanceSqr = float.NegativeInfinity;
        uint bestId = InvalidId;
        for (int i = 0; i < WaypointCount; i++)
        {
            ref readonly var waypoint = ref wpSystem.Waypoints[i];
            if (!filter.IsValid(waypoint, i))
                continue;

            var curDistanceSqr = Vector3.DistanceSquared(waypoint.Position, position);
            if (curDistanceSqr > bestDistanceSqr)
            {
                bestDistanceSqr = curDistanceSqr;
                bestId = waypoint.Id;
            }
        }
        return bestId;
    }

    public bool IsTraversable(uint waypointId) =>
        IsTraversable(wpSystem.Waypoints[idToIndex[waypointId]]);

    public static bool IsTraversable(in Waypoint waypoint) =>
        waypoint.Group == InvalidId || waypoint.WalkableIds.Length > 0 || waypoint.JumpableIds.Length > 0;

    public uint NearestTraversableId(Vector3 position) => NearestId(position, new TraversableFilter());
    private readonly struct TraversableFilter : IWaypointFilter
    {
        public bool IsValid(in Waypoint waypoint) => IsTraversable(waypoint);
    }

    public uint NearestJumpableId(Vector3 position) => NearestId(position, new JumpableFilter());
    private readonly struct JumpableFilter : IWaypointFilter
    {
        public bool IsValid(in Waypoint waypoint) => waypoint.JumpableIds.Length > 0;
    }

    public uint NearestIdOfGroup(Vector3 position, uint group) => NearestId(position, new GroupFilter(group));
    public uint FurthestIdOfGroup(Vector3 position, uint group) => FurthestId(position, new GroupFilter(group));
    private readonly struct GroupFilter(uint group) : IWaypointFilter
    {
        public bool IsValid(in Waypoint waypoint) => waypoint.Group == group;
    }

    public uint FurthestIdOfCompatible(Vector3 position, uint group) => FurthestId(position, new CompatibleGroupFilter(
        wpSystem.CompatibleGroups.GetValueOrDefault(group, []), group));
    private readonly struct CompatibleGroupFilter(uint[] compatibleGroups, uint group) : IWaypointFilter
    {
        public bool IsValid(in Waypoint waypoint) =>
            (waypoint.WalkableIds.Length > 0 || waypoint.JumpableIds.Length > 0) &&
            (waypoint.Group == group || Array.IndexOf(compatibleGroups, waypoint.Group) >= 0);
    }

    public uint NearestInvisibleTo(Vector3 position, uint otherId) => NearestId(position, new NearestInvisibleFilter(
        new ArraySegment<bool>(isVisible, idToIndex[otherId] * WaypointCount, WaypointCount)));
    private readonly struct NearestInvisibleFilter(ArraySegment<bool> isVisible) : IWaypointFilter
    {
        public bool IsValid(in Waypoint waypoint, int index) =>
            !isVisible[index] && IsTraversable(waypoint);
    }

    public uint TryRandomNextTraversable(uint fromId, out WaypointEdgeKind edgeKind) =>
        TryRandomNextTraversable(fromId, ReadOnlySpan<uint>.Empty, out edgeKind);

    public uint TryRandomNextTraversable(uint fromId, ReadOnlySpan<uint> except, out WaypointEdgeKind edgeKind)
    {
        // TODO: Investigate weird waypoint modification from random next in original code
        // The original engine would override the walkable list with the jumpable ones if it could not find
        // any walkable next waypoint. This would not happen in the original code... right?!

        // Also with the validation command not done yet I just suspect that there are no directed edges so 
        // this should degrade cleanly to: any random walkable except - otherwise any random jumpable
        // which oculd be implemented a bit more efficiently and cleaner.

        ref readonly var from = ref wpSystem.Waypoints[idToIndex[fromId]];
        var toId = random.NextOf(from.WalkableIds, except);
        if (IsTraversable(toId))
        {
            edgeKind = WaypointEdgeKind.Walkable;
            return toId;
        }

        toId = random.NextOf(from.JumpableIds, except);
        if (IsTraversable(toId))
        {
            edgeKind = WaypointEdgeKind.Jumpable;
            return toId;
        }

        edgeKind = WaypointEdgeKind.None;
        return InvalidId;
    }
}
