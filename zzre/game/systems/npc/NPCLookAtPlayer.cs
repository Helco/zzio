using System;
using System.Numerics;
using DefaultEcs.System;

using RotationMode = zzre.game.components.NPCLookAtPlayer.Mode;

namespace zzre.game.systems;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class NPCLookAtPlayer : AEntitySetSystem<float>
{
    private const float SlerpCurvature = 150f;
    private const float SlerpSpeed = 20f;
    private const float MaxSmoothRotationDistSqr = 3f;

    private Location playerLocation => playerLocationLazy.Value;
    private readonly Lazy<Location> playerLocationLazy;

    public NPCLookAtPlayer(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        var game = diContainer.GetTag<Game>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
    }

    [WithPredicate]
    private static bool IsLookAtPlayerNPCState(in components.NPCState value) => value == components.NPCState.LookAtPlayer;

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        components.NPCType npcType,
        Location location,
        ref components.NPCLookAtPlayer lookAt,
        ref components.PuppetActorMovement puppetActorMovement)
    {
        lookAt.TimeLeft -= elapsedTime;
        if (lookAt.TimeLeft < 0f)
        {
            entity.Set(components.NPCState.Script);
            return;
        }

        var playerPos = playerLocation.LocalPosition;
        var dirToPlayer = Vector3.Normalize(playerPos - location.LocalPosition);
        switch (lookAt.RotationMode)
        {
            case RotationMode.Billboard:
                location.LookAt(playerPos);
                return; // notice: no head IK / target direction

            case RotationMode.Smooth when
                Vector3.DistanceSquared(puppetActorMovement.TargetDirection, dirToPlayer) <= MaxSmoothRotationDistSqr:
                location.HorizontalSlerpIn(dirToPlayer, SlerpCurvature, SlerpSpeed * elapsedTime);
                break;

            case RotationMode.Hard:
            default:
                location.LookIn(dirToPlayer with { Y = 0.01f });
                break;
        }

        puppetActorMovement.TargetDirection = location.InnerForward;

        // TODO: Add ActorHeadIK behavior for LookAtPlayer
    }
}
