namespace zzre.game.messages;

public record struct DialogAddChoice(DefaultEcs.Entity DialogEntity, int Label, zzio.UID UID);
