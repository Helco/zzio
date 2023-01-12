using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using static ImGuiNET.ImGui;

namespace zzre.tools;

partial class ECSExplorer
{
    public delegate string? TryGetEntityNameFunc(DefaultEcs.Entity entity);

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

    private static string GetEntityName(DefaultEcs.Entity entity)
    {
        entityNamers.SortIfNecessary();
        return entityNamers
            .Select(n => n.TryGetEntityName(entity))
            .FirstOrDefault(n => n != null)
            ?? entity.ToString();
    }
}
