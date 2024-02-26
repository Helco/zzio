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

        var size = message.Size + 2;

        uiEntity.Set(new components.DialogChestPuzzle{
            DialogEntity = message.DialogEntity,
            Size = size,
            LabelExit = message.LabelExit,
            Attempts = 0,
            MinTries = 9999,
            BoardState = InitBoardState(size)
        });
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();

        CreatePrimary(uiEntity, ref puzzle);
        puzzle.Board = CreateBoard(uiEntity, ref puzzle);
    }

    private static bool[] InitBoardState(int size)
    {
        var board = new bool[size*size];
        for (int i = 0; i < size*size; i++)
            board[i] = Convert.ToBoolean(i % 2);
        return board;
    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: true, out var bgRect, opacity: 1f);
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
            .WithText($"Attempts: {puzzle.Attempts}\nMin. Tries: {puzzle.MinTries}")
            .WithLineHeight(15)
            .Build();

        return entity;
    }

    private DefaultEcs.Entity CreateBoard(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var offset = new Vector2(-1, -1) + new Vector2(-23, -23) * puzzle.Size;

        for (int row = 0; row < puzzle.Size; row++)
        {
            for (int col = 0; col < puzzle.Size; col++)
            {
                var cell = row * puzzle.Size + col;
                var IDCell = new components.ui.ElementId(cell);
                preload.CreateButton(entity)
                    .With(IDCell)
                    .With(offset + new Vector2(46 * col, 46 * row))
                    .With(new components.ui.ButtonTiles(puzzle.BoardState[cell] ? 1 : 2))
                    .With(preload.Swt000)
                    .Build();
            }
        }

        return entity;
    }

    private readonly (int row, int col)[] flipped = [
        (0, 0),
        (-1, 0),
        (0, -1),
        (1, 0),
        (0, 1)
    ];

    private void UpdateBoard(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle, int cellId)
    {
        int row = cellId / puzzle.Size;
        int col = cellId % puzzle.Size;

        Console.WriteLine($"{row}, {col}");

        foreach (var coord in flipped)
        {
            Console.WriteLine(coord);
            if (coord.row + row < puzzle.Size && coord.row + row >= 0 &&
                coord.col + col < puzzle.Size && coord.col + col >= 0)
            {
                var cell = (coord.row + row) * puzzle.Size + (coord.col + col);
                puzzle.BoardState[cell] = !puzzle.BoardState[cell];
            }
        }

        puzzle.Board.Dispose();
        puzzle.Board = CreateBoard(parent, ref puzzle);
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();
        ref var script = ref puzzle.DialogEntity.Get<components.ScriptExecution>();

        Console.WriteLine(clickedId);

        if (clickedId.InRange(new components.ui.ElementId(0), new components.ui.ElementId(puzzle.Size * puzzle.Size), out var cellId)) {
            UpdateBoard(uiEntity, ref puzzle, cellId);
        }
        else if (clickedId == IDCancel)
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
