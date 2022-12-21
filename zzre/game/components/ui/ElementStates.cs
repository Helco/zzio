namespace zzre.game.components.ui;

public record struct HoveredElement(DefaultEcs.Entity Entity, ElementId Id);

public record struct Hovered;
public record struct Active;
