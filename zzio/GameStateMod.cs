using System;
using System.IO;

namespace zzio
{
    public enum GameStateModType
    {
        DisableAttackTrigger,
        RemoveItem,
        ChangeNPCState,
        DisableTrigger,
        RemoveModel,
        SetTrigger,
        SetNPCModifier
    }

    public interface IGameStateMod
    {
        GameStateModType Type { get; }

        void Write(BinaryWriter writer);

        public static IGameStateMod ReadNew(BinaryReader r)
        {
            var type = EnumUtils.intToEnum<GameStateModType>(r.ReadInt32());
            return type switch
            {
                GameStateModType.DisableAttackTrigger => GSModDisableAttackTrigger.ReadNew(r),
                GameStateModType.RemoveItem => GSModRemoveItem.ReadNew(r),
                GameStateModType.ChangeNPCState => GSModChangeNPCState.ReadNew(r),
                GameStateModType.DisableTrigger => GSModDisableTrigger.ReadNew(r),
                GameStateModType.RemoveModel => GSModRemoveModel.ReadNew(r),
                GameStateModType.SetTrigger => GSModSetTrigger.ReadNew(r),
                GameStateModType.SetNPCModifier => GSModSetNPCModifier.ReadNew(r),
                _ => throw new InvalidDataException($"Unsupported GameState Modifier: {type}")
            };
        }
    }

    public record struct GSModDisableAttackTrigger(uint TriggerId) : IGameStateMod
    {
        public GameStateModType Type => GameStateModType.DisableAttackTrigger;

    public static GSModDisableAttackTrigger ReadNew(BinaryReader r) =>
        new GSModDisableAttackTrigger(r.ReadUInt32());

    public void Write(BinaryWriter w) => w.Write(TriggerId);
}

public record struct GSModRemoveItem(uint ModelId) : IGameStateMod
{
        public GameStateModType Type => GameStateModType.RemoveItem;

public static GSModRemoveItem ReadNew(BinaryReader r) =>
    new GSModRemoveItem(r.ReadUInt32());

public void Write(BinaryWriter w) => w.Write(ModelId);
    }

    public record struct GSModChangeNPCState(uint TriggerId, UID UID) : IGameStateMod
{
        public GameStateModType Type => GameStateModType.ChangeNPCState;

public static GSModChangeNPCState ReadNew(BinaryReader r) =>
    new GSModChangeNPCState(r.ReadUInt32(), UID.ReadNew(r));

public void Write(BinaryWriter w)
{
    w.Write(TriggerId);
    UID.Write(w);
}
    }

    public record struct GSModDisableTrigger(uint TriggerId) : IGameStateMod
{
        public GameStateModType Type => GameStateModType.DisableTrigger;

public static GSModDisableTrigger ReadNew(BinaryReader r) =>
    new GSModDisableTrigger(r.ReadUInt32());

public void Write(BinaryWriter w) => w.Write(TriggerId);
    }

    public record struct GSModRemoveModel(uint ModelId) : IGameStateMod
{
        public GameStateModType Type => GameStateModType.RemoveModel;

public static GSModRemoveModel ReadNew(BinaryReader r) =>
    new GSModRemoveModel(r.ReadUInt32());

public void Write(BinaryWriter w) => w.Write(ModelId);
    }

    public record struct GSModSetTrigger(
        uint TriggerId,
        uint II1,
        uint II2,
        uint II3,
        uint II4) : IGameStateMod
{
        public GameStateModType Type => GameStateModType.SetTrigger;

public static GSModSetTrigger ReadNew(BinaryReader r) => new GSModSetTrigger(
    r.ReadUInt32(),
    r.ReadUInt32(),
    r.ReadUInt32(),
    r.ReadUInt32(),
    r.ReadUInt32());

public void Write(BinaryWriter w)
{
    w.Write(TriggerId);
    w.Write(II1);
    w.Write(II2);
    w.Write(II3);
    w.Write(II4);
}
    }

    public record struct GSModSetNPCModifier(uint TriggerId, int Value) : IGameStateMod
{
        public GameStateModType Type => GameStateModType.SetNPCModifier;

public static GSModSetNPCModifier ReadNew(BinaryReader r) =>
    new GSModSetNPCModifier(r.ReadUInt32(), r.ReadInt32());

public void Write(BinaryWriter w)
{
    w.Write(TriggerId);
    w.Write(Value);
}
    }
}
