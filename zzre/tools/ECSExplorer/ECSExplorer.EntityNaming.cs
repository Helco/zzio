using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static ImGuiNET.ImGui;

namespace zzre.tools;

partial class ECSExplorer
{
    public delegate string? TryGetEntityNameFunc(DefaultEcs.Entity entity);
    public delegate string? TryGetEntityNameByComponentFunc<T>(in T component);

    private class EntityNamer : IComparable<EntityNamer>
    {
        public int Priority { get; init; } = 0;
        public TryGetEntityNameFunc TryGetEntityName { get; init; } = _ => null;

        public int CompareTo(EntityNamer? other)
        {
            var otherPriority = other?.Priority ?? int.MinValue;
            return otherPriority - Priority; // swapped, that high priority is at the start of the list
        }
    }

    private static readonly LazySortedList<EntityNamer> entityNamers = new();

    public static void AddEntityNamer(int prio, TryGetEntityNameFunc func)
    {
        entityNamers.Add(new()
        {
            Priority = prio,
            TryGetEntityName = func
        });
    }

    public static void AddEntityNamerByComponent<T>(int prio, TryGetEntityNameFunc func) =>
        AddEntityNamer(prio, entity => entity.IsAlive && entity.Has<T>() ? func(entity) : null);

    public static void AddEntityNamerByComponent<T>(int prio, TryGetEntityNameByComponentFunc<T> func) =>
        AddEntityNamer(prio, entity => entity.IsAlive && entity.Has<T>() ? func(entity.Get<T>()) : null);

    public static void AddEntityNamerByComponent<T>(int prio, string name) =>
        AddEntityNamer(prio, entity => entity.IsAlive && entity.Has<T>() ? name : null);

    private static string GetEntityName(DefaultEcs.Entity entity)
    {
        entityNamers.SortIfNecessary();
        return entityNamers
            .Select(n => n.TryGetEntityName(entity))
            .FirstOrDefault(n => n != null)
            ?? entity.ToString();
    }
}
