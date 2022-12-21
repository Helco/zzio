using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace zzio;

public abstract class InventoryCard
{
    public CardId cardId;
    public uint atIndex;
    public UID dbUID;
    public uint amount;
    public bool isInUse;

    public static InventoryCard ReadNew(BinaryReader r)
    {
        var cardId = new CardId(r.ReadUInt32());
        InventoryCard card = cardId.Type switch
        {
            CardType.Item => new InventoryItem(),
            CardType.Spell => new InventorySpell(),
            CardType.Fairy => new InventoryFairy(),
            _ => throw new InvalidDataException($"Invalid inventory card type: {cardId.Type}")
        };
        card.cardId = cardId;
        card.atIndex = r.ReadUInt32();
        card.dbUID = UID.ReadNew(r);
        card.amount = r.ReadUInt32();
        card.isInUse = r.ReadBoolean();
        card.ReadSub(r);
        return card;
    }

    public virtual void Write(BinaryWriter w)
    {
        w.Write(cardId.raw);
        w.Write(atIndex);
        dbUID.Write(w);
        w.Write(amount);
        w.Write(isInUse);
    }

    protected virtual void ReadSub(BinaryReader r) { }
}

public class InventoryItem : InventoryCard
{
}

public class InventorySpell : InventoryCard
{
    public uint usageCounter;
    public uint mana;

    protected override void ReadSub(BinaryReader r)
    {
        usageCounter = r.ReadUInt32();
        mana = r.ReadUInt32();
    }

    public override void Write(BinaryWriter w)
    {
        base.Write(w);
        w.Write(usageCounter);
        w.Write(mana);
    }
}

public class InventoryFairy : InventoryCard
{
    public const int SpellSlotCount = 4;

    public uint levelChangeCount;
    public uint level;
    public uint unknown1;
    public uint unknown2;
    public uint xpChangeCount;
    public uint xp;
    public readonly SpellReq[] spellReqs = new SpellReq[SpellSlotCount];
    public readonly int[] spellIndices = new int[SpellSlotCount] { -1, -1, -1, -1 };
    public int slotIndex;
    public ZZPermSpellStatus status;
    public readonly byte[] unknown3 = new byte[20];
    public uint currentMHP;
    public uint mhpChangeCount;
    public string name = "";

    // unsaved
    public uint maxMHP;
    public float moveSpeed;
    public float jumpPower;
    public float jumpMana = 10000f;
    public float maxJumpMana = 10000f;
    public float criticalHit;

    protected override void ReadSub(BinaryReader r)
    {
        levelChangeCount = r.ReadUInt32();
        level = r.ReadUInt32();
        unknown1 = r.ReadUInt32();
        unknown2 = r.ReadUInt32();
        xpChangeCount = r.ReadUInt32();
        xp = r.ReadUInt32();
        for (int i = 0; i < spellReqs.Length; i++)
            spellReqs[i] = SpellReq.ReadNew(r);
        for (int i = 0; i < spellIndices.Length; i++)
            spellIndices[i] = r.ReadInt32();
        slotIndex = r.ReadInt32();
        status = EnumUtils.intToEnum<ZZPermSpellStatus>(r.ReadInt32());
        r.Read(unknown3.AsSpan());
        mhpChangeCount = r.ReadUInt32();
        currentMHP = r.ReadUInt32();
        name = r.ReadZString();
    }

    public override void Write(BinaryWriter w)
    {
        base.Write(w);
        w.Write(levelChangeCount);
        w.Write(level);
        w.Write(unknown1);
        w.Write(unknown2);
        w.Write(xpChangeCount);
        w.Write(xp);
        Array.ForEach(spellReqs, r => r.Write(w));
        Array.ForEach(spellIndices, w.Write);
        w.Write(slotIndex);
        w.Write((int)status);
        w.Write(unknown3);
        w.Write(mhpChangeCount);
        w.Write(currentMHP);
        w.WriteZString(name);
    }
}

public record struct SpellReq(ZZClass class0, ZZClass class1 = ZZClass.None, ZZClass class2 = ZZClass.None) : IEnumerable<ZZClass>
{
    public static SpellReq ReadNew(BinaryReader r) => new(
        EnumUtils.intToEnum<ZZClass>(r.ReadByte()),
        EnumUtils.intToEnum<ZZClass>(r.ReadByte()),
        EnumUtils.intToEnum<ZZClass>(r.ReadByte()));

    public void Write(BinaryWriter w)
    {
        w.Write((byte)class0);
        w.Write((byte)class1);
        w.Write((byte)class2);
    }

    public IEnumerator<ZZClass> GetEnumerator()
    {
        if (class0 != ZZClass.None) yield return class0;
        if (class1 != ZZClass.None) yield return class1;
        if (class2 != ZZClass.None) yield return class2;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public enum ZZPermSpellStatus
{
    None = 0,
    Poisoned,
    Cursed,
    Burned,
    Frozen,
    Silenced
}
