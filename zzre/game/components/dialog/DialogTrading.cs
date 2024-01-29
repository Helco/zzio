﻿using System;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.components.ui;

public struct DialogTrading
{
    public DefaultEcs.Entity DialogEntity;
    public ItemRow Currency;
    public ItemRow? Purchase;
    public List<(int price, UID uid)> CardTrades;
    public Dictionary<components.ui.ElementId, ItemRow> CardPurchaseButtons;
    public DefaultEcs.Entity Profile;
}
