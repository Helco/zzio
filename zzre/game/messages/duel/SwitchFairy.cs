namespace zzre.game.messages;

public readonly record struct SwitchFairy(
    DefaultEcs.Entity Participant,
    int ToSlot = -1);
