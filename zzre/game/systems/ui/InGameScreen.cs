using System;
using System.Numerics;
using DefaultEcs.System;
using zzio;

namespace zzre.game.systems.ui;

public static class InGameScreen
{
    public static readonly Vector2 Mid = -new Vector2(320, 240);

    public static readonly components.ui.ElementId IDClose = new(1000);
    public static readonly components.ui.ElementId IDOpenDeck = new(1001);
    public static readonly components.ui.ElementId IDOpenRunes = new(1002);
    public static readonly components.ui.ElementId IDOpenFairybook = new(1003);
    public static readonly components.ui.ElementId IDOpenMap = new(1004);
    public static readonly components.ui.ElementId IDSaveGame = new(1005);

    private record struct TabInfo(components.ui.ElementId Id, int PosX, int TileI, UID TooltipUID);
    private static readonly TabInfo[] Tabs = new TabInfo[]
    {
        new(IDOpenDeck,      PosX: 553, TileI: 21, TooltipUID: new(0xdeadbeef)),
        new(IDOpenRunes,     PosX: 427, TileI: 12, TooltipUID: new(0x6636B4A1)),
        new(IDOpenFairybook, PosX: 469, TileI: 15, TooltipUID: new(0x8D1BBCA1)),
        new(IDOpenMap,       PosX: 511, TileI: 18, TooltipUID: new(0xC51E6991))
    };

    public static void CreateTopButtons(UIPreloader preload, in DefaultEcs.Entity parent, components.ui.ElementId curTab)
    {
        preload.CreateButton(parent)
            .With(IDClose)
            .With(Mid + new Vector2(606, 3))
            .With(new components.ui.ButtonTiles(24, 25))
            .With(preload.Btn002)
            .WithTooltip(0xAD3AACA1)
            .Build();

        preload.CreateButton(parent)
            .With(IDSaveGame)
            .With(Mid + new Vector2(384, 3))
            .With(new components.ui.ButtonTiles(26, 27))
            .With(preload.Btn002)
            .WithTooltip(0x7113B8A1)
            .Build();

        foreach (var tab in Tabs)
        {
            var tabButton = preload.CreateButton(parent)
                .With(tab.Id)
                .With(Mid + new Vector2(tab.PosX, 3))
                .With(new components.ui.ButtonTiles(tab.TileI, tab.TileI + 1, tab.TileI + 2))
                .With(preload.Btn002)
                .WithTooltip(tab.TooltipUID)
                .Build();

            if (tab.Id == curTab)
            {
                tabButton.Set<components.ui.Active>();
                tabButton.Remove<components.ui.TooltipUID>();
            }
        }
    }
}
