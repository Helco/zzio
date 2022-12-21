namespace zzre.game.components.ui;

public record struct ButtonTiles(
    int Normal,
    int Hovered = -1,
    int Active = -1,
    int ActiveHovered = -1);
