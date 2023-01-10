namespace zzre.tools;
using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using zzre.game;
using zzre.imgui;

using static ImGuiNET.ImGui;

internal partial class ECSExplorerWindow
{
    private readonly ITagContainer diContainer;
    private readonly ZanzarahWindow zzWindow;
    private readonly HashSet<DefaultEcs.Entity> openEntities = new();

    public Window Window { get; }
    private Zanzarah Zanzarah => zzWindow.Zanzarah;

    public ECSExplorerWindow(ITagContainer diContainer, ZanzarahWindow zzWindow)
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
            if (!TreeNodeEx(entity.ToString(), ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow))
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
}
