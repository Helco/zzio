namespace zzre.game.messages.ui;

public readonly record struct ExecuteUIScript(
    zzio.InventoryItem Item,
    DefaultEcs.Entity DeckSlotEntity
);
