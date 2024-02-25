using System;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogChestPuzzle : ui.BaseScreen<components.DialogChestPuzzle, messages.DialogChestPuzzle>
{
    private static readonly components.ui.ElementId IDCancel = new(1000);
    private static readonly components.ui.ElementId IDWin = new(1001);

    private static readonly UID UIDCancel = new(0xD45B15B1);
    private static readonly UID UIDBoxOfTricks = new(0x6588C491);

    private readonly MappedDB db;
    private readonly IDisposable resetUISubscription;

    private readonly int boardSize = 3;

    public DialogChestPuzzle(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        db = diContainer.GetTag<MappedDB>();

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
            Size = message.Size,
            LabelExit = message.LabelExit,
        });
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();

        CreatePrimary(uiEntity, ref puzzle);
        CreateBoard(uiEntity, ref puzzle);
    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: true, out var bgRect);
        preload.CreateDialogBackground(entity, animateOverlay: true, out var _);
        preload.CreateSingleDialogButton(entity, UIDCancel, IDCancel, bgRect, buttonOffsetY: -45f);
        preload.CreateSingleDialogButton(entity, UIDCancel, IDWin, bgRect, buttonOffsetY: -85f);

        preload.CreateLabel(entity)
            .With(bgRect.Min + new Vector2(20, 20))
            .With(preload.Fnt001)
            .WithText(db.GetText(UIDBoxOfTricks).Text)
            .Build();

        preload.CreateLabel(entity)
            .With(bgRect.Min + new Vector2(25, 120))
            .With(preload.Fnt000)
            .WithText($"Attempts: 0\nMin. Tries: 9999")
            .WithLineHeight(14)
            .Build();

        return entity;
    }

    private Vector2 boardOrigin = new(-70, -70);
    private DefaultEcs.Entity CreateBoard(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        for (int row = 0; row < boardSize; row++)
        {
            for (int col = 0; col < boardSize; col++)
            {
                var IDCell = new components.ui.ElementId(row * boardSize + col);
                preload.CreateButton(entity)
                    .With(IDCell)
                    .With(boardOrigin + new Vector2(46 * col, 46 * row))
                    .With(new components.ui.ButtonTiles(1))
                    .With(preload.Swt000)
                    .Build();
            }
        }

        return entity;
    }
    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();
        ref var script = ref puzzle.DialogEntity.Get<components.ScriptExecution>();

        Console.WriteLine(clickedId);

        if (clickedId == IDCancel)
        {
            World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g003.wav"));
            puzzle.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
        else if (clickedId == IDWin)
        {
            script.CurrentI = script.LabelTargets[puzzle.LabelExit];
            puzzle.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogChestPuzzle component)
    {
    }
}
