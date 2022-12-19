using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace zzio
{
    public class Savegame
    {
        private const int LocationBlockSize = 8;

        public ZZVersion version = ZZVersion.CreateDefault();
        public string name = "";
        public int secondsPlayed;
        public int progress;
        public int sceneId = -1;
        public int entryId = -1;
        public readonly Dictionary<string, List<IGameStateMod>> gameState = new();
        public readonly List<InventoryCard> inventory = new();
        public uint pixiesHolding;
        public uint pixiesCatched;
        public readonly GlobalVar[] globalVars = GlobalVar.CreateSet();
        public uint switchGameMinMoves = 9999;

        public void Add(string scene, IGameStateMod mod)
        {
            if (!gameState.TryGetValue(scene, out var sceneMods))
                gameState.Add(scene, sceneMods = new List<IGameStateMod>());
            sceneMods.Add(mod);
        }

        public static Savegame ReadNew(BinaryReader r)
        {
            Savegame sg = new()
            {
                version = ZZVersion.ReadNew(r),
                name = r.ReadZString(),
                secondsPlayed = r.ReadInt32(),
                progress = r.ReadInt32()
            };
            if (r.ReadUInt32() != LocationBlockSize)
                throw new InvalidDataException("Invalid size of savegame location block");
            sg.sceneId = r.ReadInt32();
            sg.entryId = r.ReadInt32();

            var sceneCount = r.ReadUInt32();
            sg.gameState.EnsureCapacity((int)sceneCount);
            for (uint i = 0; i < sceneCount; i++)
            {
                var sceneName = r.ReadZString();
                var modCount = r.ReadInt32();
                var mods = new List<IGameStateMod>(modCount);
                for (int j = 0; j < modCount; j++)
                    mods.Add(IGameStateMod.ReadNew(r));
                sg.gameState[sceneName] = mods;
            }

            var itemCount = r.ReadUInt32();
            sg.inventory.EnsureCapacity((int)itemCount);
            for (uint i = 0; i < itemCount; i++)
                sg.inventory.Add(InventoryCard.ReadNew(r));

            sg.pixiesHolding = r.ReadUInt32();
            sg.pixiesCatched = r.ReadUInt32();

            if (r.ReadInt32() != GlobalVar.Count)
                throw new InvalidDataException("Invalid global variable count");
            for (int i = 0; i < sg.globalVars.Length; i++)
                sg.globalVars[i] = GlobalVar.ReadNew(r);

            sg.switchGameMinMoves = r.ReadUInt32();
            return sg;
        }

        public void Write(BinaryWriter w)
        {
            version.Write(w);
            w.WriteZString(name);
            w.Write(LocationBlockSize);
            w.Write(sceneId);
            w.Write(entryId);

            w.Write(gameState.Count);
            foreach (var (sceneName, mods) in gameState)
            {
                w.WriteZString(sceneName);
                w.Write(mods.Count);
                foreach (var mod in mods)
                    mod.Write(w);
            }

            w.Write(inventory.Count);
            foreach (var card in inventory)
                card.Write(w);

            w.Write(pixiesHolding);
            w.Write(pixiesCatched);

            w.Write(globalVars.Length);
            foreach (var var in globalVars)
                var.Write(w);

            w.Write(switchGameMinMoves);
        }
    }

    public record struct GlobalVar(uint ChangeCount, uint Value)
    {
        public const int Count = 49;

        public static GlobalVar ReadNew(BinaryReader r) =>
            new(r.ReadUInt32(), r.ReadUInt32());

        public void Write(BinaryWriter w)
        {
            w.Write(ChangeCount);
            w.Write(Value);
        }

        public static GlobalVar[] CreateSet()
        {
            var random = new Random();
            return Enumerable
                .Range(0, Count)
                .Select(i => new GlobalVar(ChangeCount: (uint)random.Next(0, 2 << 16), Value: 0))
                .ToArray();
        }
    }
}
