using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zzio
{
    public enum ZZDBModule
    {
        Fairy = 0,
        Text = 1,
        Spell = 2,
        Item = 3,
        Npc = 4,
        Dialog = 5
    }

    public abstract class ZZDBMappedRow
    {
        protected ZZDBMappedRow(ZZMappedDatabase db, ZZDBRow row)
        {
            this.db = db;
            this.row = row;
        }

        protected ZZMappedDatabase db;
        protected ZZDBRow row;

        protected object col(int idx) { return idx < row.columns.Length ? row.columns[idx].data : (uint)0; }

        public UInt32 UID { get { return row.uid; } }
        public ZZDBModule Module { get { return (ZZDBModule)(row.uid & 7); } }
    }
    
    public class ZZDBMappedFairyRow : ZZDBMappedRow
    {
        public ZZDBMappedFairyRow(ZZMappedDatabase db, ZZDBRow row) : base(db, row) { }

        public string Mesh { get { return col(0) as string; } }
        public string Name { get { return (db[((ZZDBUUID)col(1)).uid] as ZZDBMappedTextRow).Text; } }
        public uint Class0 { get { return (uint)col(2); } }
        public uint CardId { get { return (uint)col(3); } }
        public uint Unknown { get { return (uint)col(4); } }
        public uint Level0 { get { return (uint)col(5); } }
        public uint Level1 { get { return (uint)col(6); } }
        public uint Level2 { get { return (uint)col(7); } }
        public uint Level3 { get { return (uint)col(8); } }
        public uint Level4 { get { return (uint)col(9); } }
        public uint Level5 { get { return (uint)col(10); } }
        public uint Level6 { get { return (uint)col(11); } }
        public uint Level7 { get { return (uint)col(12); } }
        public uint Level8 { get { return (uint)col(13); } }
        public uint Level9 { get { return (uint)col(14); } }
        public string Info { get { return (db[((ZZDBUUID)col(15)).uid] as ZZDBMappedTextRow).Text; } }
        public uint MHP { get { return (uint)col(16); } }
        public uint EvolCId { get { return (uint)col(17); } }
        public uint EvolVar { get { return (uint)col(18); } }
        public uint MovSpeed { get { return (uint)col(19); } }
        public uint JumpPower { get { return (uint)col(20); } }
        public uint CriticalHit { get { return (uint)col(21); } }
        public uint Sphere { get { return (uint)col(22); } }
        public uint Glow { get { return (uint)col(23); } }
        public uint LevelUp { get { return (uint)col(24); } }
        public uint Voice { get { return (uint)col(25); } }
        public uint Class1 { get { return (uint)col(26); } }
    }

    public class ZZDBMappedTextRow : ZZDBMappedRow
    {
        public ZZDBMappedTextRow(ZZMappedDatabase db, ZZDBRow row) : base(db, row) { }

        public string Text { get { return col(0) as string; } }
        public uint Group { get { return (uint)col(1); } }
        public string Define { get { return col(2) as string; } }
    }

    public class ZZDBMappedSpellRow : ZZDBMappedRow
    {
        public ZZDBMappedSpellRow(ZZMappedDatabase db, ZZDBRow row) : base(db, row) { }

        public string Name { get { return (db[((ZZDBUUID)col(0)).uid] as ZZDBMappedTextRow).Text; } }
        public uint Type { get { return (uint)col(1); } }
        public uint CardId { get { return (uint)col(2); } }
        public uint PriceA { get { return (uint)col(3); } }
        public uint PriceB { get { return (uint)col(4); } }
        public uint PriceC { get { return (uint)col(5); } }
        public string Info { get { return (db[((ZZDBUUID)col(6)).uid] as ZZDBMappedTextRow).Text; } }
        public uint Mana { get { return (uint)col(7); } }
        public uint Loadup { get { return (uint)col(8); } }
        public uint Unknown { get { return (uint)col(9); } }
        public uint MissileEffect { get { return (uint)col(10); } }
        public uint ImpactEffect { get { return (uint)col(11); } }
        public uint Damage { get { return (uint)col(12); } }
        public uint Behaviour { get { return (uint)col(13); } }
    }

    public class ZZDBMappedItemRow : ZZDBMappedRow
    {
        public ZZDBMappedItemRow(ZZMappedDatabase db, ZZDBRow row) : base(db, row) { }

        public string Name { get { return (db[((ZZDBUUID)col(0)).uid] as ZZDBMappedTextRow).Text; } }
        public uint CardId { get { return (uint)col(1); } }
        public string Info { get { return (db[((ZZDBUUID)col(2)).uid] as ZZDBMappedTextRow).Text; } }
        public uint Special { get { return (uint)col(3); } }
        public string Script { get { return col(4) as string; } }
        public uint Unknown { get { return (uint)col(5); } }
    }

    public class ZZDBMappedNpcRow : ZZDBMappedRow
    {
        public ZZDBMappedNpcRow(ZZMappedDatabase db, ZZDBRow row) : base(db, row) { }

        public string Name { get { return (db[((ZZDBUUID)col(0)).uid] as ZZDBMappedTextRow).Text; } }
        public string Script1 { get { return col(1) as string; } }
        public string Script2 { get { return col(2) as string; } }
        public string Script3 { get { return col(3) as string; } }
        public string Script4 { get { return col(4) as string; } }
        public string Script5 { get { return col(5) as string; } }
        public string Unknown { get { return col(6) as string; } }
    }

    public class ZZDBMappedDialogRow : ZZDBMappedRow
    {
        public ZZDBMappedDialogRow(ZZMappedDatabase db, ZZDBRow row) : base(db, row) { }

        public string Text { get { return col(0) as string; } }
        public uint Npc { get { return (uint)col(1); } }
        public string Voice { get { return col(2) as string; } }
    }

    public class ZZMappedDatabase
    {
        public static int[][] rawColumnMapping =
        {
            new int[] { 6, 3, 37, 4, 19, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 5, 33, 34, 35, 36, 46, 47, 49, 48, 50, 30, 7 },
            new int[] { 0, 1, 2 },
            new int[] { 3, 44, 4, 21, 22, 23, 5, 38, 39, 19, 41, 42, 43, 45 },
            new int[] { 3, 4, 5, 31, 32, 19 },
            new int[] { 3, 24, 25, 26, 27, 28, 19 },
            new int[] { 0, 29, 30 }
        };

        ZZDatabase[] modules;

        public ZZMappedDatabase (ZZDatabase[] modules)
        {
            if (modules.Length != 6)
                throw new Exception("Invalid number of database modules");
            for (int i=0; i<6; i++)
            {
                if (modules[i].rows[0].columns.Length != rawColumnMapping[i].Length)
                    throw new Exception("Invalid number of columns in database module " + (i+1));
            }
            this.modules = modules;
        }

        public ZZDatabase[] Modules { get { return modules; } }

        public ZZDBMappedRow this[UInt32 uid]
        {
            get
            {
                UInt32 module = uid & 7;
                if (module > 5)
                    return null;
                ZZDBRow row = modules[module].getRowByUID(uid);
                if (row.uid != uid)
                    return null;
                switch(module)
                {
                    case (0): { return new ZZDBMappedFairyRow(this, row); }
                    case (1): { return new ZZDBMappedTextRow(this, row); }
                    case (2): { return new ZZDBMappedSpellRow(this, row); }
                    case (3): { return new ZZDBMappedItemRow(this, row); }
                    case (4): { return new ZZDBMappedNpcRow(this, row); }
                    case (5): { return new ZZDBMappedDialogRow(this, row); }
                }
                return null;
            }
        }

        public ZZDBMappedRow byUID (UInt32 uid)
        {
            return this[uid];
        }

        public ZZDBMappedRow byCardId(UInt32 cardId)
        {
            UInt32 type = (cardId >> 8) & 0xff;
            if (type > 2 || (cardId & 0xff) != 0)
                return null;
            ZZDatabase db = modules[type == 0 ? 3 : (type == 1 ? 2 : 0)];
            foreach (ZZDBRow row in db.rows)
            {
                switch(type)
                {
                    case (0):
                        {
                            ZZDBMappedItemRow item = new ZZDBMappedItemRow(this, row);
                            if (item.CardId == cardId)
                                return item;
                        }break;
                    case (1):
                        {
                            ZZDBMappedSpellRow spell = new ZZDBMappedSpellRow(this, row);
                            if (spell.CardId == cardId)
                                return spell;
                        }break;
                    case (2):
                        {
                            ZZDBMappedFairyRow fairy = new ZZDBMappedFairyRow(this, row);
                            if (fairy.CardId == cardId)
                                return fairy;
                        }break;
                }
            }
            return null;
        }
    }
}
