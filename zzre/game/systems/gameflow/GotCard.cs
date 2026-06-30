namespace zzre.game.systems;
using System;
using DefaultEcs.System;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class GotCard : ISystem<float>
{
    private readonly DefaultEcs.World ecsWorld;
    private readonly UI ui;
    private readonly IDisposable gotCardDisposable;
    private messages.GotCard lastMessage;

    public bool IsEnabled { get; set; } = true;

    public GotCard(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ui = diContainer.GetTag<UI>();
        gotCardDisposable = ecsWorld.Subscribe<messages.GotCard>(HandleGotCard);
    }

    public void Dispose()
    {
        gotCardDisposable?.Dispose();
    }
     
    private void HandleGotCard(in messages.GotCard message)
    {
        lastMessage = message;
        ecsWorld.Set(components.GameFlow.GotCard);
    }

    public void Update(float _)
    {
        if (!IsEnabled || ecsWorld.Get<components.GameFlow>() != components.GameFlow.GotCard)
            return;
        ecsWorld.Set(components.GameFlow.Normal);
        ui.Publish(new messages.ui.OpenGotCard(lastMessage.UID, lastMessage.Amount));
    }
}
