namespace zzre.game.messages.ui;

public record struct GameScreenOpened;
public record struct GameScreenClosed;

public record struct OpenDeck;

public record struct OpenGotCard(zzio.UID UID, int Amount);
