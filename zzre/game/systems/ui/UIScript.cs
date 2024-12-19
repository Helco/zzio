using System;
using DefaultEcs.System;
using DefaultEcs.Command;
using zzio.db;

namespace zzre.game.systems.ui;

public partial class UIScript : BaseScript<UIScript>
{
    private readonly MappedDB db;
    private readonly EntityCommandRecorder recorder;
    private readonly IDisposable executeUIScriptDisposable;

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

    public UIScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
    {
        db = diContainer.GetTag<MappedDB>();
        recorder = diContainer.GetTag<EntityCommandRecorder>();
        executeUIScriptDisposable = World.Subscribe<messages.ui.ExecuteUIScript>(HandleExecuteUIScript);
    }

    public override void Dispose()
    {
        base.Dispose();
        executeUIScriptDisposable.Dispose();
    }

    private static void ModifyWizform(DefaultEcs.Entity scriptEntity, ModifyWizformType type, int value)
    {
        ref var script = ref scriptEntity.Get<components.ui.UIScript>();
        ref var slot = ref script.DeckSlotEntity.Get<components.ui.Slot>();

        Console.WriteLine($"Not implemented: ModifyWizform, {scriptEntity}, {type}, {value}");
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
        scriptEntity.Set(new components.ui.UIScript(message.DeckSlotEntity));
        var scriptEntityRecord = recorder.Record(scriptEntity);
        scriptEntityRecord.Set(new components.ScriptExecution(db.GetItem(message.Item.dbUID).Script));
    }

    [Update]
    private void Update(in DefaultEcs.Entity scriptEntity, ref components.ScriptExecution execution)
    {
        Continue(scriptEntity, ref execution);
        scriptEntity.Dispose();
    }
}
