namespace zzre.game.messages;

public readonly record struct LoadActor(
    DefaultEcs.Entity AsEntity,
    string ActorName,
    AssetLoadPriority Priority);
