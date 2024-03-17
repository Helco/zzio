﻿namespace zzre.game.components;

public struct DuelParticipant(DefaultEcs.Entity entity)
{
    public readonly DefaultEcs.Entity OverworldEntity = entity;
    public DefaultEcs.Entity ActiveFairy = default;
    public int ActiveSlot = -1;
}
