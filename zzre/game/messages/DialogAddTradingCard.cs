namespace zzre.game.messages;

public record struct DialogAddTradingCard(DefaultEcs.Entity DialogEntity, int Price, zzio.UID UID);
