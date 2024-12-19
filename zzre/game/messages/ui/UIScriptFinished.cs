namespace zzre.game.messages.ui;

public readonly record struct UIScriptFinished(
    DefaultEcs.Entity DeckSlotEntity,
    bool ItemConsumed
);
