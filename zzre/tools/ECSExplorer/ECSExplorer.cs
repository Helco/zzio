namespace zzre.tools;
using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using zzre.game;
using zzre.imgui;

using static ImGuiNET.ImGui;

internal partial class ECSExplorer
{
    private readonly ITagContainer diContainer;
    private readonly ZanzarahWindow zzWindow;

    public Window Window { get; }
    private Zanzarah Zanzarah => zzWindow.Zanzarah;

    static ECSExplorer()
    {
        AddStandardEntityNaming();
    }

    public ECSExplorer(ITagContainer diContainer, ZanzarahWindow zzWindow)
    {
        this.diContainer = diContainer;
        this.zzWindow = zzWindow;
        Window = diContainer.GetTag<WindowContainer>().NewWindow("ECS Explorer");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 400, 800);
        zzWindow.Window.OnClose += Window.Dispose;

        Window.OnContent += HandleContent;
    }

    private void HandleContent()
    {
        if (Zanzarah.CurrentGame == null)
            return;

        var entityContentRenderer = new EntityContentRenderer();

        foreach (var entity in Zanzarah.CurrentGame.PlayerEntity.World)
        {
            if (!TreeNodeEx(GetEntityName(entity), ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow))
                continue;
            if (!BeginTable("components", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg))
            {
                TreePop();
                continue;
            }
            TableSetupColumn("Name");
            TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
            entity.ReadAllComponents(entityContentRenderer);
            EndTable();
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
