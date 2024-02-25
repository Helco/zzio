using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogChestPuzzle : ui.BaseScreen<components.DialogChestPuzzle, messages.DialogChestPuzzle>
{
    private static readonly components.ui.ElementId IDExit = new(1000);

    private static readonly UID UIDExit = new(0xF7DFDC21);

    private readonly IDisposable resetUISubscription;

    public DialogChestPuzzle(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        resetUISubscription = World.Subscribe<messages.DialogResetUI>(HandleResetUI);
        OnElementDown += HandleElementDown;
    }

    public override void Dispose()
    {
        base.Dispose();
        resetUISubscription.Dispose();
    }

    private void HandleResetUI(in messages.DialogResetUI message)
    {
        foreach (var entity in Set.GetEntities())
            entity.Dispose();
    }

    protected override void HandleOpen(in messages.DialogChestPuzzle message)
    {
        message.DialogEntity.Set(components.DialogState.ChestPuzzle);

        World.Publish(new messages.DialogResetUI(message.DialogEntity));
        var uiEntity = World.CreateEntity();
        uiEntity.Set(new components.Parent(message.DialogEntity));

        uiEntity.Set(new components.DialogChestPuzzle{
            DialogEntity = message.DialogEntity,
        });
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();

        CreatePrimary(uiEntity, ref puzzle);
    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: true, out var bgRect);
        preload.CreateSingleDialogButton(entity, UIDExit, IDExit, bgRect);

        return entity;
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();

        if (clickedId == IDExit) {
            World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g003.wav"));
            puzzle.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogChestPuzzle component)
    {
    }
}
