using System.Numerics;
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

    private record struct TabInfo(components.ui.ElementId Id, int PosX, int TileI, UID TooltipUID, StdItemId Item);
    private static readonly TabInfo[] Tabs =
    [
        new(IDOpenDeck,      PosX: 553, TileI: 21, TooltipUID: new(0x6659B4A1), StdItemId.FairyBag),
        new(IDOpenRunes,     PosX: 427, TileI: 12, TooltipUID: new(0x6636B4A1), StdItemId.RuneFairyGarden),
        new(IDOpenFairybook, PosX: 469, TileI: 15, TooltipUID: new(0x8D1BBCA1), StdItemId.FairyBook),
        new(IDOpenMap,       PosX: 511, TileI: 18, TooltipUID: new(0xC51E6991), StdItemId.MapFairyGarden)
    ];

    public static void CreateTopButtons(UIBuilder preload, in DefaultEcs.Entity parent, Inventory inventory, components.ui.ElementId curTab)
    {
        preload.CreateButton(parent)
            .With(IDClose)
            .With(Mid + new Vector2(606, 3))
            .With(new components.ui.ButtonTiles(24, 25))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0xAD3AACA1)
            .Build();

        preload.CreateButton(parent)
            .With(IDSaveGame)
            .With(Mid + new Vector2(384, 3))
            .With(new components.ui.ButtonTiles(26, 27))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0x7113B8A1)
            .Build();

        foreach (var tab in Tabs)
        {
            if (!inventory.Contains(tab.Item))
                continue;
            var tabButton = preload.CreateButton(parent)
                .With(tab.Id)
                .With(Mid + new Vector2(tab.PosX, 3))
                .With(new components.ui.ButtonTiles(tab.TileI, tab.TileI + 1, tab.TileI + 2))
                .With(UIPreloadAsset.Btn002)
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
