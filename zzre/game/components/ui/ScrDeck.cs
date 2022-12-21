using System;

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
    public Inventory Inventory;
    public DefaultEcs.Entity SummaryBackground;
    public DefaultEcs.Entity SpellBackground;
    public DefaultEcs.Entity ListSlider;
    public DefaultEcs.Entity FairyHoverSummary;
    public DefaultEcs.Entity[] ListTabs;
    public DefaultEcs.Entity[] ListButtons;
    public DefaultEcs.Entity[] ListUsedMarkers;
    public DefaultEcs.Entity[] ListSummaries;
    public DefaultEcs.Entity[] DeckSlotParents;
}
