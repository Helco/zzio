namespace zzre.game.messages;

public readonly struct CreaturePlaceToGround
{
    public readonly DefaultEcs.Entity Entity;

    public CreaturePlaceToGround(DefaultEcs.Entity entity) => Entity = entity;
}
