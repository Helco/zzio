namespace zzre.game.components.ui;

public struct ScrDeck
{
    public enum Tab
    {
        None,
        Fairies,
        Items,
        SupportSpells,
        AttackSpells
    }

    public Tab ActiveTab;
    public bool IsGridMode;
    public int Scroll;
    public DefaultEcs.Entity SummaryBackground;
    public DefaultEcs.Entity SpellBackground;

    public DefaultEcs.Entity[] TabButtons;
    public DefaultEcs.Entity ListSlider;

    public DefaultEcs.Entity[] DeckSlots;
    public DefaultEcs.Entity[] ListSlots;

    public DefaultEcs.Entity StatsTitle;
    public DefaultEcs.Entity StatsDescriptions;
    public DefaultEcs.Entity StatsLights;
    public DefaultEcs.Entity StatsLevel;

    public DefaultEcs.Entity LastHovered;
    public DefaultEcs.Entity DraggedCard;
    public DefaultEcs.Entity DraggedOverlay;
}
