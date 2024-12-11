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
    public DefaultEcs.Entity ListSlider;
    public DefaultEcs.Entity[] ListTabs;
    public DefaultEcs.Entity[] ListButtons;
    public DefaultEcs.Entity[] ListUsedMarkers;
    public DefaultEcs.Entity[] ListSummaries;
    public DefaultEcs.Entity[] DeckSlotParents;
    public DefaultEcs.Entity LastHovered;
    public DefaultEcs.Entity StatsTitle;
    public DefaultEcs.Entity StatsDescriptions;
    public DefaultEcs.Entity StatsLights;
    public DefaultEcs.Entity StatsLevel;
}
