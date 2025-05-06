namespace zzre.game.components.ui;

public record struct UIScript(
    DefaultEcs.Entity DeckSlotEntity,
    bool ItemConsumed
);
