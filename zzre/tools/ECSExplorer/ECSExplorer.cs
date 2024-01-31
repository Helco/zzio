namespace zzre.tools;
using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using zzre.game;
using zzre.game.components;
using zzre.imgui;

using static ImGuiNET.ImGui;

internal interface IECSWindow
{
    Window Window { get; }
    IEnumerable<(string name, DefaultEcs.World)> GetWorlds();
}

internal partial class ECSExplorer
{
    private readonly ITagContainer diContainer;
    private readonly IECSWindow ecsWindow;

    public Window Window { get; }

    static ECSExplorer()
    {
        AddStandardEntityNaming();
        AddStandardEntityGrouping();
    }

    public ECSExplorer(ITagContainer diContainer, IECSWindow ecsWindow)
    {
        this.diContainer = diContainer;
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

    private void HandleContentFor(DefaultEcs.World world)
    {
        var entityContentRenderer = new EntityContentRenderer();

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
    }

    private class LazySortedList<T> : List<T>
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
