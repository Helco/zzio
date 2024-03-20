using DefaultEcs.System;

namespace zzre.game.systems;

public sealed partial class FairyVisibilityByCamera : ISystem<float>
{
    [Configuration(Description = "Minimal distance for player fairy to be visible")]
    private float PlayerMinDistance = 0.3f;

    private readonly DefaultEcs.World ecsWorld;
    public bool IsEnabled { get; set; }

    public FairyVisibilityByCamera(ITagContainer diContainer)
    {
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
    }

    public void Dispose()
    {
    }

    public void Update(float _)
    {
        var duelCamera = ecsWorld.Get<components.ActiveCamera>().System as DuelCamera;
        var playerEntity = ecsWorld.Get<components.PlayerEntity>().Entity;
        var activeFairy = playerEntity.Get<components.DuelParticipant>().ActiveFairy;
        if (!activeFairy.IsAlive || duelCamera is null)
            return;
        var actorParts = activeFairy.Get<components.ActorParts>();
        var newVisibility = duelCamera.CurCamDistance > PlayerMinDistance
            ? components.Visibility.Visible
            : components.Visibility.Invisible;
        actorParts.Body.Set(newVisibility);
        actorParts.Wings?.Set(newVisibility);
    }
}
