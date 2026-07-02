namespace zzre.tools;
using IconFonts;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using zzre.game.components;
using zzre.imgui;
using static ImGuiNET.ImGui;

internal interface IECSWindow
{
    Window Window { get; }
    IEnumerable<(string name, DefaultEcs.World)> GetWorlds();
}

internal sealed partial class ECSExplorer
{
    private readonly IECSWindow ecsWindow;

    public Window Window { get; }

    static ECSExplorer()
    {
        AddStandardEntityNaming();
        AddStandardEntityGrouping();
    }

    public ECSExplorer(ITagContainer diContainer, IECSWindow ecsWindow)
    {
        this.ecsWindow = ecsWindow;
        Window = diContainer.GetTag<WindowContainer>().NewWindow("ECS Explorer");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 400, 800);
        ecsWindow.Window.OnClose += Window.Dispose;

        Window.OnContent += HandleContent;
    }

    private void HandleContent()
    {
        BeginTabBar("Worlds", ImGuiTabBarFlags.AutoSelectNewTabs);
        foreach (var (name, world) in ecsWindow.GetWorlds())
        {
            if (BeginTabItem(name))
            {
                HandleContentFor(world);
                EndTabItem();
            }
        }
        EndTabBar();
    }

    private static void HandleContentFor(DefaultEcs.World world)
    {
        var entityContentRenderer = new EntityContentRenderer();

        HandleWorldComponentsFor(world);

        var children = world.ToLookup(e => e.TryGet<Parent>().GetValueOrDefault().Entity);

        var groups = children[default].ToLookup(GetEntityGroup);

        foreach (var group in groups)
        {
            if (group.Key == null)
                continue;

            if (!TreeNodeEx($"{group.Key} ({group.Count()})", ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow))
                continue;
            foreach (var entity in group)
                HandleEntity(entity);
            TreePop();
        }
        if (groups.Contains(null))
        {
            foreach (var entity in groups[null])
                HandleEntity(entity);
        }

        void HandleEntity(DefaultEcs.Entity entity)
        {
            HandleEntityProtection(entity);
            if (!TreeNodeEx(GetEntityName(entity), ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow))
                return;
            if (!BeginTable("components", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg))
            {
                TreePop();
                return;
            }
            TableSetupColumn("Name");
            TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            entity.ReadAllComponents(entityContentRenderer);
            EndTable();

            if (children.Contains(entity))
            {
                foreach (var child in children[entity])
                    HandleEntity(child);
            }

            TreePop();
        }

        [Conditional("DEBUG")]
        static void HandleEntityProtection(DefaultEcs.Entity entity)
        {
            bool isProtected = entity.Has<DebugProtected>();

            PushID(entity.GetHashCode());
            if (Button(isProtected ? ForkAwesome.Lock : ForkAwesome.Unlock))
            {
                if (isProtected)
                    entity.Remove<DebugProtected>();
                else
                    entity.Set<DebugProtected>();
            }
            PopID();
            SameLine();
        }
    }

    private static void HandleWorldComponentsFor(DefaultEcs.World world)
    {
        var hasAny = new CountComponentReader();
        world.ReadAllWorldComponents(hasAny);
        if (hasAny.ComponentCount == 0)
            return;

        var entityContentRenderer = new EntityContentRenderer();
        if (!TreeNodeEx("World", ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow))
            return;
        if (!BeginTable("components", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg))
        {
            TreePop();
            return;
        }
        TableSetupColumn("Name");
        TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        world.ReadAllWorldComponents(entityContentRenderer);
        EndTable();
        TreePop();
    }

    private sealed class CountComponentReader : DefaultEcs.Serialization.IComponentReader
    {
        public int ComponentCount { get; private set; }
        public void OnRead<T>(in T component, in DefaultEcs.Entity componentOwner)
        {
            ComponentCount++;
        }
    }

    private sealed class LazySortedList<T> : List<T>
    {
        private bool isSorted = true;

        public new void Add(T value)
        {
            base.Add(value);
            isSorted = false;
        }

        public void SortIfNecessary()
        {
            if (isSorted)
                return;
            isSorted = true;
            Sort();
        }
    }
}
