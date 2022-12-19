namespace zzre.tools;
using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.Serialization;
using ImGuiNET;
using zzre.game;
using zzre.imgui;

using static ImGuiNET.ImGui;

internal class ECSExplorerWindow
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

        foreach (var entity in Zanzarah.CurrentGame.PlayerEntity.World)
        {
            if (!TreeNodeEx(entity.ToString(), ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow))
                continue;
            RenderEntityContent(entity);
            TreePop();
        }
    }

    private class EntityContentRenderer : IComponentReader
    {
        public void OnRead<T>(in T component, in DefaultEcs.Entity componentOwner)
        {
            if (TreeNodeEx(typeof(T).Name, ImGuiTreeNodeFlags.Leaf))
                TreePop();
        }
    }

    private void RenderEntityContent(DefaultEcs.Entity entity)
    {
        entity.ReadAllComponents(new EntityContentRenderer());
    }
}
