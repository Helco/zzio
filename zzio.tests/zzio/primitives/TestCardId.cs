using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestCardId
{
    [Test]
    public void fromRaw()
    {
        CardId fairy0 = new(512);
        Assert.AreEqual(512, fairy0.raw);
        Assert.AreEqual(CardType.Fairy, fairy0.Type);
        Assert.AreEqual(0, fairy0.EntityId);

        CardId fairy76 = new(4981248);
        Assert.AreEqual(4981248, fairy76.raw);
        Assert.AreEqual(CardType.Fairy, fairy76.Type);
        Assert.AreEqual(76, fairy76.EntityId);

        CardId spell105 = new(6881536);
        Assert.AreEqual(6881536, spell105.raw);
        Assert.AreEqual(CardType.Spell, spell105.Type);
        Assert.AreEqual(105, spell105.EntityId);
    }

    [Test]
    public void fromComponents()
    {
        CardId fairy0 = new(CardType.Fairy, 0);
        Assert.AreEqual(512, fairy0.raw);

        CardId fairy76 = new(CardType.Fairy, 76);
        Assert.AreEqual(4981248, fairy76.raw);

        CardId spell105 = new(CardType.Spell, 105);
        Assert.AreEqual(6881536, spell105.raw);
    }

    [Test]
    public void toString()
    {
        CardId fairy0 = new(CardType.Fairy, 0);
        Assert.AreEqual("Fairy:0", fairy0.ToString());

        CardId item49 = new(CardType.Item, 49);
        Assert.AreEqual("Item:49", item49.ToString());

        CardId spell105 = new(6881536);
        Assert.AreEqual("Spell:105", spell105.ToString());
    }
}