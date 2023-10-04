namespace zzre.game.messages;

public readonly record struct UnlockDoor(
    DefaultEcs.Entity doorEntity,
    DefaultEcs.Entity lockEntity,
    StdItemId keyItemId);
