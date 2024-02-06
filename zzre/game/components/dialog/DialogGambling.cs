using System;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.components;

public struct DialogGambling
{
    public DefaultEcs.Entity DialogEntity;
    public ItemRow Currency;
    public SpellRow? Purchase;
    public List<int?> Cards;
    public List<SpellRow?> SelectedCards;
    public Rect bgRect;
    public Dictionary<components.ui.ElementId, SpellRow> CardPurchaseButtons;
    public DefaultEcs.Entity Profile;
    public float? RowAnimationTimeLeft;
}
