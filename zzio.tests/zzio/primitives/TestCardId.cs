using NUnit.Framework;

namespace zzio.tests.primitives;

[TestFixture]
public class TestCardId
{
    [Test]
    public void fromRaw()
    {
        CardId fairy0 = new(512);
        Assert.That(fairy0.raw, Is.EqualTo(512));
        Assert.That(fairy0.Type, Is.EqualTo(CardType.Fairy));
        Assert.That(fairy0.EntityId, Is.EqualTo(0));

        CardId fairy76 = new(4981248);
        Assert.That(fairy76.raw, Is.EqualTo(4981248));
        Assert.That(fairy76.Type, Is.EqualTo(CardType.Fairy));
        Assert.That(fairy76.EntityId, Is.EqualTo(76));

        CardId spell105 = new(6881536);
        Assert.That(spell105.raw, Is.EqualTo(6881536));
        Assert.That(spell105.Type, Is.EqualTo(CardType.Spell));
        Assert.That(spell105.EntityId, Is.EqualTo(105));
    }

    [Test]
    public void fromComponents()
    {
        CardId fairy0 = new(CardType.Fairy, 0);
        Assert.That(fairy0.raw, Is.EqualTo(512));

        CardId fairy76 = new(CardType.Fairy, 76);
        Assert.That(fairy76.raw, Is.EqualTo(4981248));

        CardId spell105 = new(CardType.Spell, 105);
        Assert.That(spell105.raw, Is.EqualTo(6881536));
    }

    [Test]
    public void toString()
    {
        CardId fairy0 = new(CardType.Fairy, 0);
        Assert.That(fairy0.ToString(), Is.EqualTo("Fairy:0"));

        CardId item49 = new(CardType.Item, 49);
        Assert.That(item49.ToString(), Is.EqualTo("Item:49"));

        CardId spell105 = new(6881536);
        Assert.That(spell105.ToString(), Is.EqualTo("Spell:105"));
    }
}