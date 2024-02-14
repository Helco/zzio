using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static ImGuiNET.ImGui;

namespace zzre.tools;

partial class ECSExplorer
{
    public delegate string? TryGetEntityGroupFunc(DefaultEcs.Entity entity);
    public delegate string? TryGetEntityGroupByComponentFunc<T>(in T component);

    private class EntityGrouper : IComparable<EntityGrouper>
    {
        public int Priority { get; init; }
        public TryGetEntityGroupFunc TryGetEntityGroup { get; init; } = _ => null;

        public int CompareTo(EntityGrouper? other)
        {
            var otherPriority = other?.Priority ?? int.MinValue;
            return otherPriority - Priority; // swapped, that high priority is at the start of the list
        }
    }

    private static readonly LazySortedList<EntityGrouper> entityGroupers = new();

    public static void AddEntityGrouper(int prio, TryGetEntityGroupFunc func)
    {
        entityGroupers.Add(new()
        {
            Priority = prio,
            TryGetEntityGroup = func
        });
    }

    public static void AddEntityGrouperByComponent<T>(int prio, TryGetEntityGroupFunc func) =>
        AddEntityGrouper(prio, entity => entity.IsAlive && entity.Has<T>() ? func(entity) : null);

    public static void AddEntityGrouperByComponent<T>(int prio, TryGetEntityGroupByComponentFunc<T> func) =>
        AddEntityGrouper(prio, entity => entity.IsAlive && entity.TryGet<T>(out var comp) ? func(comp) : null);

    public static void AddEntityGrouperByComponent<T>(int prio, string name) =>
        AddEntityGrouper(prio, entity => entity.IsAlive && entity.Has<T>() ? name : null);

    private static string? GetEntityGroup(DefaultEcs.Entity entity)
    {
        entityGroupers.SortIfNecessary();
        return entityGroupers
            .Select(n => n.TryGetEntityGroup(entity))
            .FirstOrDefault(n => n != null);
    }
}
