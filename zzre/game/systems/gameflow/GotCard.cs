namespace zzre.game.systems;
using System;
using DefaultEcs.System;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class GotCard : AEntitySetSystem<float>
{
    private readonly Game game;
    private readonly UI ui;
    private readonly IDisposable gotCardDisposable;
    private messages.GotCard lastMessage;

    public GotCard(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        game = diContainer.GetTag<Game>();
        ui = diContainer.GetTag<UI>();
        gotCardDisposable = World.Subscribe<messages.GotCard>(HandleGotCard);
    }

    public override void Dispose()
    {
        base.Dispose();
        gotCardDisposable?.Dispose();
    }
     
    private void HandleGotCard(in messages.GotCard message)
    {
        lastMessage = message;
        game.PlayerEntity.Set(components.GameFlow.GotCard);
    }

    [WithPredicate]
    private static bool IsInGameFlow(in components.GameFlow flow) => flow == components.GameFlow.GotCard;

    [Update]
    private void Update(in DefaultEcs.Entity entity)
    {
        entity.Set(components.GameFlow.Normal);
        ui.Publish(new messages.ui.OpenGotCard(lastMessage.UID, lastMessage.Amount));
    }
}
