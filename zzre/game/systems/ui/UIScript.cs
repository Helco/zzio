using System;
using DefaultEcs.System;
using DefaultEcs.Command;
using zzio.db;
using zzio;

namespace zzre.game.systems.ui;

public partial class UIScript : BaseScript<UIScript>
{
    private readonly MappedDB db;
    private readonly EntityCommandRecorder recorder;
    private readonly IDisposable executeUIScriptDisposable;
    protected readonly Zanzarah zanzarah;
    protected readonly UI ui;
    protected Inventory inventory => zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();

    public enum ModifyWizformType
    {
        Heal = 0,
        AddXP = 1,
        ClearStatusEffects = 2,
        Transform = 7,
        AddNearLevelXP = 8,
        // 9-13: Set Unknown
        // 14: Add Unknown
        Revive = 16,
        FillMana = 17,
        Rename = 18,
    }

    private void PlayChime()
    {
        World.Publish(new messages.SpawnSample("resources/audio/sfx/specials/_s021.wav"));
    }

    public UIScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
    {
        db = diContainer.GetTag<MappedDB>();
        zanzarah = diContainer.GetTag<Zanzarah>();
        ui = diContainer.GetTag<UI>();
        recorder = diContainer.GetTag<EntityCommandRecorder>();
        executeUIScriptDisposable = World.Subscribe<messages.ui.ExecuteUIScript>(HandleExecuteUIScript);
    }

    public override void Dispose()
    {
        base.Dispose();
        executeUIScriptDisposable.Dispose();
    }

    private bool ModifyWizform(DefaultEcs.Entity scriptEntity, ModifyWizformType type, int value)
    {
        ref var script = ref scriptEntity.Get<components.ui.UIScript>();
        ref var slot = ref script.DeckSlotEntity.Get<components.ui.Slot>();
        var fairy = (InventoryFairy)slot.card!;

        switch (type)
        {
            case ModifyWizformType.Heal:
                if (fairy.currentMHP == fairy.maxMHP) return false;
                PlayChime();
                fairy.currentMHP += (uint)value;
                if (fairy.currentMHP > fairy.maxMHP)
                    fairy.currentMHP = fairy.maxMHP;
                return true;
            case ModifyWizformType.AddXP:
                // TODO: is there a chime?
                inventory.AddXP(fairy, (uint)value);
                return true;
            case ModifyWizformType.ClearStatusEffects:
                PlayChime();
                fairy.status = ZZPermSpellStatus.None;
                return true;
            case ModifyWizformType.Transform:
                fairy.cardId = new CardId(CardType.Fairy, value);
                fairy.dbUID = db.GetFairy(value).Uid;
                // TODO: correctly handle evolution, including name, fairy stats
                return true;
            case ModifyWizformType.AddNearLevelXP:
                // TODO: is there a chime?
                var nearLevel = inventory.GetLevelupXP(fairy) - fairy.xp + 1;
                if (nearLevel != null)
                    inventory.AddXP(fairy, (uint)nearLevel);
                // TODO: investigate golden carrot behaviour on level 59 & 60
                return true;
            case ModifyWizformType.Revive:
                if (fairy.currentMHP != 0)
                {
                    ui.Publish(new messages.ui.Notification(db.GetText(new UID(0x3D422781)).Text));
                    return false;
                }
                // TODO: is there a chime?
                // TODO: Determine the correct revive hp factor
                fairy.currentMHP = (uint)(fairy.maxMHP * 0.5);
                return true;
            case ModifyWizformType.FillMana:
                PlayChime();
                inventory.FillMana(fairy);
                return true;
            case ModifyWizformType.Rename:
                Console.WriteLine($"Not implemented: ModifyWizform: Rename");
                return false;
            default:
                throw new NotImplementedException($"Unimplemented ModifyWizformType: {type}");
        }
    }

    private static bool IfIsWizform(DefaultEcs.Entity scriptEntity, int fairyI)
    {
        ref var script = ref scriptEntity.Get<components.ui.UIScript>();
        ref var slot = ref script.DeckSlotEntity.Get<components.ui.Slot>();
        if (slot.card!.cardId.EntityId == fairyI)
            return true;
        return false;
    }

    private void HandleExecuteUIScript(in messages.ui.ExecuteUIScript message)
    {
        var scriptEntity = World.CreateEntity();
        scriptEntity.Set(new components.ui.UIScript(message.DeckSlotEntity, false));
        var scriptEntityRecord = recorder.Record(scriptEntity);
        scriptEntityRecord.Set(new components.ScriptExecution(db.GetItem(message.Item.dbUID).Script));
    }

    [Update]
    private void Update(in DefaultEcs.Entity scriptEntity, ref components.ScriptExecution execution)
    {
        Continue(scriptEntity, ref execution);
        ref var script = ref scriptEntity.Get<components.ui.UIScript>();
        World.Publish(new messages.ui.UIScriptFinished(script.DeckSlotEntity, script.ItemConsumed));
        scriptEntity.Dispose();
    }
}
