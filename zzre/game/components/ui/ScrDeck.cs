using System;

namespace zzre.game.components.ui
{
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
        public Inventory Inventory;
        public DefaultEcs.Entity SummaryBackground;
        public DefaultEcs.Entity SpellBackground;
        public DefaultEcs.Entity ListSlider;
        public DefaultEcs.Entity DeckContentParent;
        public DefaultEcs.Entity[] DeckSlotParents;
    }
}
