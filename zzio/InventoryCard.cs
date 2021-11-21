using System;
using System.IO;

namespace zzio
{
    public abstract class InventoryCard
    {
        public CardId cardId;
        public uint atIndex;
        public UID dbUID;
        public uint amount;
        public bool isInUse;

        protected void ReadCore(BinaryReader r)
        {
            cardId = new CardId(r.ReadUInt32());
            atIndex = r.ReadUInt32();
            dbUID = UID.ReadNew(r);
            amount = r.ReadUInt32();
            isInUse = r.ReadBoolean();
        }

        protected void WriteCore(BinaryWriter w)
        {
            w.Write(cardId.raw);
            w.Write(atIndex);
            dbUID.Write(w);
            w.Write(amount);
            w.Write(isInUse);
        }
    }

    public class InventoryItem : InventoryCard
    {
        public static InventoryItem ReadNew(BinaryReader r)
        {
            var item = new InventoryItem();
            item.ReadCore(r);
            return item;
        }

        public void Write(BinaryWriter w) => WriteCore(w);
    }

    public class InventorySpell : InventoryCard
    {
        public uint usageCount;
        public uint mana;

        public static InventorySpell ReadNew(BinaryReader r)
        {
            var spell = new InventorySpell();
            spell.usageCount = r.ReadUInt32();
            spell.mana = r.ReadUInt32();
            return spell;
        }

        public void Write(BinaryWriter w)
        {
            WriteCore(w);
            w.Write(usageCount);
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
        public readonly uint[] spellIndices = new uint[SpellSlotCount];
        public uint slotIndex;
        public ZZPermSpellStatus status;
        public readonly byte[] unknown3 = new byte[20];
        public uint mhp;
        public string name = "";

        public static InventoryFairy ReadNew(BinaryReader r)
        {
            var fairy = new InventoryFairy();
            fairy.ReadCore(r);
            fairy.levelChangeCount = r.ReadUInt32();
            fairy.level = r.ReadUInt32();
            fairy.unknown1 = r.ReadUInt32();
            fairy.unknown2 = r.ReadUInt32();
            fairy.xpChangeCount = r.ReadUInt32();
            fairy.xp = r.ReadUInt32();
            for (int i = 0; i < fairy.spellReqs.Length; i++)
                fairy.spellReqs[i] = SpellReq.ReadNew(r);
            for (int i = 0; i < fairy.spellIndices.Length; i++)
                fairy.spellIndices[i] = r.ReadUInt32();
            fairy.slotIndex = r.ReadUInt32();
            fairy.status = EnumUtils.intToEnum<ZZPermSpellStatus>(r.ReadInt32());
            r.Read(fairy.unknown3.AsSpan());
            fairy.mhp = r.ReadUInt32();
            fairy.name = r.ReadZString();
            return fairy;
        }

        public void Write(BinaryWriter w)
        {
            WriteCore(w);
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
            w.Write(mhp);
            w.WriteZString(name);
        }
    }

    public record struct SpellReq(ZZClass class0, ZZClass class1, ZZClass class2)
    {
        public static SpellReq ReadNew(BinaryReader r) => new SpellReq(
            EnumUtils.intToEnum<ZZClass>(r.ReadByte()),
            EnumUtils.intToEnum<ZZClass>(r.ReadByte()),
            EnumUtils.intToEnum<ZZClass>(r.ReadByte()));

        public void Write(BinaryWriter w)
        {
            w.Write((byte)class0);
            w.Write((byte)class1);
            w.Write((byte)class2);
        }
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
}
