using System.IO;
using NUnit.Framework;

namespace zzio.tests;

[TestFixture]
public class TestSavegame
{
    /// <summary>Write -> Read must reproduce the savegame, especially the
    /// game state mods whose type prefix Write historically dropped</summary>
    [Test]
    public void RoundtripWithGameStateMods()
    {
        var savegame = new Savegame
        {
            sceneId = 2801,
            entryId = 3,
            name = "roundtrip",
            secondsPlayed = 123,
            progress = 4,
            pixiesHolding = 2,
            pixiesCatched = 1,
            switchGameMinMoves = 7
        };
        savegame.gameState["sc_2801"] =
        [
            new GSModRemoveItem(11),
            new GSModRemoveModel(22),
            new GSModSetTrigger(33, 44, 45, 46, 47),
            new GSModChangeNPCState(55, new UID(0xCAFE)),
            new GSModDisableAttackTrigger(66),
            new GSModDisableTrigger(77),
            new GSModSetNPCModifier(88, -1),
        ];
        var item = new InventoryItem
        {
            cardId = new CardId(CardType.Item, 21),
            atIndex = 0,
            dbUID = new UID(0xBEEF),
            amount = 1,
            isInUse = false
        };
        savegame.inventory.Add(item);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            savegame.Write(writer);
        stream.Position = 0;

        Savegame result;
        using (var reader = new BinaryReader(stream))
            result = Savegame.ReadNew(reader);

        Assert.That(result.sceneId, Is.EqualTo(2801));
        Assert.That(result.entryId, Is.EqualTo(3));
        Assert.That(result.secondsPlayed, Is.EqualTo(123));
        Assert.That(result.progress, Is.EqualTo(4));
        Assert.That(result.gameState, Contains.Key("sc_2801"));
        var mods = result.gameState["sc_2801"];
        Assert.That(mods, Has.Count.EqualTo(7));
        Assert.That(mods[0], Is.EqualTo(new GSModRemoveItem(11)));
        Assert.That(mods[1], Is.EqualTo(new GSModRemoveModel(22)));
        Assert.That(mods[2], Is.EqualTo(new GSModSetTrigger(33, 44, 45, 46, 47)));
        Assert.That(mods[3], Is.EqualTo(new GSModChangeNPCState(55, new UID(0xCAFE))));
        Assert.That(mods[4], Is.EqualTo(new GSModDisableAttackTrigger(66)));
        Assert.That(mods[5], Is.EqualTo(new GSModDisableTrigger(77)));
        Assert.That(mods[6], Is.EqualTo(new GSModSetNPCModifier(88, -1)));
        Assert.That(result.inventory, Has.Count.EqualTo(1));
        Assert.That(result.inventory[0].cardId, Is.EqualTo(item.cardId));
        Assert.That(result.inventory[0].amount, Is.EqualTo(1u));
        Assert.That(result.pixiesHolding, Is.EqualTo(2u));
        Assert.That(result.pixiesCatched, Is.EqualTo(1u));
        Assert.That(result.switchGameMinMoves, Is.EqualTo(7u));
    }
}
