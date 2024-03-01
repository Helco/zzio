using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogChestPuzzle : ui.BaseScreen<components.DialogChestPuzzle, messages.DialogChestPuzzle>
{
    private static readonly components.ui.ElementId IDCancel = new(1000);
    private static readonly components.ui.ElementId IDNext = new(1001);

    private static readonly UID UIDCancel = new(0xD45B15B1);
    private static readonly UID UIDNext = new(0xCABAD411);

    private static readonly UID UIDAttempts = new(0x7B48CC11);
    private static readonly UID UIDMinTries = new(0xEE63D011);
    private static readonly UID UIDBoxOfTricks = new(0x6588C491);
    private static readonly UID UIDChestOpened = new(0xF798C91);

    private readonly MappedDB db;
    private readonly zzio.Savegame savegame;
    private readonly IDisposable resetUISubscription;

    public DialogChestPuzzle(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        db = diContainer.GetTag<MappedDB>();
        savegame = diContainer.GetTag<zzio.Savegame>();

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

        preload.CreateDialogBackground(uiEntity, animateOverlay: true, out var bgRect, opacity: 1f);

        uiEntity.Set(new components.DialogChestPuzzle{
            DialogEntity = message.DialogEntity,
            Size = message.Size,
            LabelExit = message.LabelExit,
            NumAttempts = 0,
            BoardState = InitBoardState(message.Size),
            BgRect = bgRect
        });
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();

        preload.CreateLabel(uiEntity)
            .With(puzzle.BgRect.Min + new Vector2(20, 20))
            .With(preload.Fnt001)
            .WithText(db.GetText(UIDBoxOfTricks).Text)
            .Build();

        puzzle.Board = CreateBoard(uiEntity, ref puzzle);
        puzzle.Attempts = preload.CreateLabel(uiEntity)
            .With(puzzle.BgRect.Min + new Vector2(25, 120))
            .With(preload.Fnt000)
            .WithText(FormatAttempts(ref puzzle))
            .WithLineHeight(15)
            .Build();
        puzzle.Action = preload.CreateSingleDialogButton(uiEntity, UIDCancel, IDCancel, puzzle.BgRect, buttonOffsetY: -45f);
    }

    private static bool[] InitBoardState(int size)
    {
        var board = new bool[size*size];
        for (int i = 0; i < size*size; i++)
            board[i] = Convert.ToBoolean(i % 2);
        return board;
    }

    private string FormatAttempts(ref components.DialogChestPuzzle puzzle) =>
        $"{db.GetText(UIDAttempts).Text}: {puzzle.NumAttempts}\n{db.GetText(UIDMinTries).Text}: {savegame.switchGameMinMoves}";

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
                var button = preload.CreateButton(entity)
                    .With(IDCell)
                    .With(offset + new Vector2(46 * col, 46 * row))
                    .With(new components.ui.ButtonTiles(puzzle.BoardState[cell] ? 1 : 2))
                    .With(preload.Swt000)
                    .Build();
                button.Set(button.Get<Rect>().GrownBy(new Vector2(1, 1))); // No gaps
                button.Set(new components.ui.Silent());
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
        puzzle.NumAttempts += 1;
        World.Publish(new messages.SpawnSample("resources/audio/sfx/gui/_g000.wav"));

        int row = cellId / puzzle.Size;
        int col = cellId % puzzle.Size;

        foreach (var coord in flipped)
        {
            if (coord.row + row < puzzle.Size && coord.row + row >= 0 &&
                coord.col + col < puzzle.Size && coord.col + col >= 0)
            {
                var cell = (coord.row + row) * puzzle.Size + (coord.col + col);
                puzzle.BoardState[cell] = !puzzle.BoardState[cell];
            }
        }

        if (puzzle.BoardState.All(x => x) || puzzle.BoardState.All(x => !x))
            Succeed(parent, ref puzzle);

        puzzle.Board.Dispose();
        puzzle.Board = CreateBoard(parent, ref puzzle);
        puzzle.Attempts.Set(new components.ui.Label(FormatAttempts(ref puzzle)));
    }

    private void Succeed(DefaultEcs.Entity parent, ref components.DialogChestPuzzle puzzle)
    {
        World.Publish(new messages.SpawnSample($"resources/audio/sfx/specials/_s022.wav"));

        preload.CreateLabel(parent)
            .With(new Vector2(0, -126))
            .With(components.ui.FullAlignment.Center)
            .With(preload.Fnt001)
            .WithText(db.GetText(UIDChestOpened).Text)
            .Build();

        if (savegame.switchGameMinMoves > puzzle.NumAttempts)
            savegame.switchGameMinMoves = puzzle.NumAttempts;

        puzzle.LockBoard = true;

        puzzle.Action.Dispose();
        puzzle.Action = preload.CreateSingleDialogButton(parent, UIDNext, IDNext, puzzle.BgRect, buttonOffsetY: -45f);
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var puzzle = ref uiEntity.Get<components.DialogChestPuzzle>();
        ref var script = ref puzzle.DialogEntity.Get<components.ScriptExecution>();

        if (!puzzle.LockBoard && clickedId.InRange(new components.ui.ElementId(0), new components.ui.ElementId(puzzle.Size * puzzle.Size), out var cellId)) {
            UpdateBoard(uiEntity, ref puzzle, cellId);
        }
        else if (clickedId == IDCancel)
        {
            World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g003.wav"));
            puzzle.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
        else if (clickedId == IDNext)
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
