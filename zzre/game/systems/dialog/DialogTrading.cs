using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogTrading : ui.BaseScreen<components.ui.DialogTrading, messages.DialogTrading>
{
    private static readonly components.ui.ElementId IDExit = new(1000);

    private readonly MappedDB db;
    private readonly IDisposable resetUISubscription;
    private readonly IDisposable addTradingCardSubscription;

    public DialogTrading(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        db = diContainer.GetTag<MappedDB>();

        resetUISubscription = World.Subscribe<messages.DialogResetUI>(HandleResetUI);
        addTradingCardSubscription = World.Subscribe<messages.DialogAddTradingCard>(HandleAddTradingCard);
        OnElementDown += HandleElementDown;
    }

    public override void Dispose()
    {
        base.Dispose();
        resetUISubscription.Dispose();
        addTradingCardSubscription.Dispose();
    }

    private void HandleResetUI(in messages.DialogResetUI message)
    {
        foreach (var entity in Set.GetEntities())
            entity.Dispose();
    }

    protected override void HandleOpen(in messages.DialogTrading message)
    {
        message.DialogEntity.Set(components.DialogState.Talk);

        World.Publish(new messages.DialogResetUI(message.DialogEntity));
        var uiEntity = World.CreateEntity();
        uiEntity.Set(new components.Parent(message.DialogEntity));
        uiEntity.Set(new components.ui.DialogTrading(message.DialogEntity));
        ref var trading = ref uiEntity.Get<components.ui.DialogTrading>();

        preload.CreateDialogBackground(uiEntity, animateOverlay: false, out var bgRect);
        CreateSingleButton(uiEntity, new UID(0xF7DFDC21), IDExit, bgRect);
        trading.Topbar = CreateTopbar(uiEntity, message.DialogUID, bgRect);
    }

    private DefaultEcs.Entity CreateTopbar(DefaultEcs.Entity parent, UID cardUID, Rect bgRect)
    {
        ItemRow card = db.GetItem(cardUID);
        var cards = db.Items.OrderBy(itemRow => itemRow.CardId.EntityId).ToArray();
        var cardI = Array.FindIndex(cards, c => c.Name == card.Name);

        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        var amountOwned = inventory.CountCards(card.CardId);

        var entity = preload.CreateLabel(parent)
            .With(new Vector2(-60, -170))
            .With(preload.Fnt000)
            .WithText($"You have {{{3000 + cardI}}}x{amountOwned}")
            .Build();
        return entity;
    }

    private void HandleAddTradingCard(in messages.DialogAddTradingCard message)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var trading = ref uiEntity.Get<components.ui.DialogTrading>();
        trading.TradingCards += 1;
        var cards = db.Items.OrderBy(itemRow => itemRow.CardId.EntityId).ToArray();
        var card = db.GetItem(message.UID);

        var element = new components.ui.ElementId(trading.TradingCards);
        var button = preload.CreateButton(uiEntity)
            .With(element)
            .With(new Vector2(-205, -175 + 55*trading.TradingCards))
            .With(new components.ui.ButtonTiles(card.CardId.EntityId))
            .With(preload.Itm000)
            .Build();

        preload.CreateLabel(uiEntity)
            .With(new Vector2(-155, -159 + 55*trading.TradingCards))
            .WithText(card.Name)
            .With(preload.Fnt002)
            .Build();

        preload.CreateLabel(uiEntity)
            .With(new Vector2(120, -4+16-175 + 55*trading.TradingCards))
            .WithText($"{{0*x}}{message.Price}")
            .With(preload.Fnt000)
            .Build();

        preload.CreateButton(uiEntity)
            .With(new components.ui.ElementId(10 + trading.TradingCards))
            .With(new Vector2(160, -175 + 55*trading.TradingCards))
            .With(new components.ui.ButtonTiles(20, 21))
            .With(preload.Btn001)
            .Build();
    }

    private const float ButtonOffsetY = -50f;
    private void CreateSingleButton(DefaultEcs.Entity parent, UID textUID, components.ui.ElementId elementId, Rect bgRect)
    {
        preload.CreateButton(parent)
            .With(elementId)
            .With(new Vector2(bgRect.Center.X, bgRect.Max.Y + ButtonOffsetY))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(components.ui.FullAlignment.TopCenter)
            .With(preload.Btn000)
            .WithLabel()
            .With(preload.Fnt000)
            .WithText(textUID)
            .Build();

        // TODO: Set cursor position in dialog trading
    }
    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        if (clickedId == IDExit) {
            var uiEntity = Set.GetEntities()[0];
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.DialogTrading component)
    {
    }
}
