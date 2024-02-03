using System;
using DefaultEcs.System;
using zzio;
using zzre.materials;

namespace zzre.game.systems;

public partial class ActorLighting : AEntitySetSystem<float>
{
    private static readonly FColor DefaultColor = FColor.White;
    private const float AnimDuration = 0.25f;
    private const float FadeDistance = 0.3f; // normalized
    private const float DimToWhite = 0.1f;
    private const float DimToBlack = 0.2f;

    private readonly GameTime time;

    public ActorLighting(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        time = diContainer.GetTag<GameTime>();
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        Location location,
        in components.ActorParts actorParts,
        ref components.ActorLighting actorLighting,
        in components.FindActorFloorCollisions floorCollisionConfig)
    {
        components.ActorFloorCollision collision = default;
        var targetColor = entity.TryGet(out collision) ? collision.Color : DefaultColor;
        var scaledDistance = (location.GlobalPosition.Y - collision.Point.Y) / floorCollisionConfig.MaxDistance;
        if (scaledDistance is < 0f or > 1f)
            targetColor = DefaultColor;
        else if (scaledDistance > FadeDistance)
        {
            scaledDistance = (scaledDistance - FadeDistance) / (1f - FadeDistance);
            targetColor = targetColor * (1f - scaledDistance) + DefaultColor * scaledDistance;
        }

        var animTime = Math.Clamp((time.TotalElapsed - actorLighting.LastTime) / AnimDuration, 0f, 1f);
        actorLighting.CurColor += (targetColor - actorLighting.CurColor) * animTime;
        actorLighting.LastTime = time.TotalElapsed;

        var nextColor = actorLighting.CurColor;
        nextColor = nextColor * (1f - DimToWhite) + FColor.White * DimToWhite;
        nextColor = nextColor * (1f - DimToBlack) + FColor.Black * DimToBlack;
        nextColor.a = 1f;
        SetColor(nextColor, actorParts.Body);
        SetColor(nextColor, actorParts.Wings);
    }

    private void SetColor(FColor color, DefaultEcs.Entity? entity)
    {
        if (entity is null || !entity.Value.TryGet<ModelMaterial[]>(out var materials))
            return;
        foreach (var material in materials)
            material.Tint.Value = color;
    }


}
