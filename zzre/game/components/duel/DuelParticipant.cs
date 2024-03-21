namespace zzre.game.components;

public struct DuelParticipant(DefaultEcs.Entity entity)
{
    public readonly DefaultEcs.Entity OverworldEntity = entity;
    public readonly DefaultEcs.Entity[] Fairies = new DefaultEcs.Entity[Inventory.FairySlotCount];
    public int ActiveSlot = -1;

    public readonly DefaultEcs.Entity ActiveFairy =>
        ActiveSlot < 0 ? default : Fairies[ActiveSlot];
}
