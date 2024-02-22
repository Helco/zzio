using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using DefaultEcs.Command;
using DefaultEcs.System;
using zzio;
using zzio.scn;
using zzio.db;
using zzio.vfs;

namespace zzre.game.systems;

public partial class DialogScript : BaseScript<DialogScript>
{
    public enum SpecialInventoryCheck
    {
        HasFivePixies = 0,
        HasAFairy,
        HasAtLeastNFairies,
        HasFairyOfClass
    }

    public enum SceneObjectType
    {
        Platforms = 0,
        Items
    }

    public enum SubGameType
    {
        ChestPuzzle = 0,
        ElfGame
    }

    public enum TalkMode
    {
        Exit = 0,
        YesNo,
        Continue
    }

    private const int UpperLetterboxHeight = 20;
    private const int LowerLetterboxHeight = 100;
    private const int SegmentsPerAddSay = 8;

    private readonly Random Random = Random.Shared;
    private readonly IResourcePool resourcePool;
    private readonly MappedDB db;
    private readonly UI ui;
    private readonly Game game;
    private readonly zzio.Savegame savegame;
    private readonly EntityCommandRecorder recorder;
    private readonly IDisposable startDialogDisposable;
    private readonly IDisposable removedDisposable;
    private readonly IDisposable sceneLoadedDisposable;

    private Scene scene = null!;
    private DefaultEcs.Entity dialogEntity;
    private EntityRecord RecordDialogEntity() => recorder.Record(dialogEntity);
    private DefaultEcs.Entity NPCEntity => dialogEntity.Get<components.DialogNPC>().Entity;

    public DialogScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
    {
        World.SetMaxCapacity<components.DialogState>(1);
        resourcePool = diContainer.GetTag<IResourcePool>();
        db = diContainer.GetTag<MappedDB>();
        ui = diContainer.GetTag<UI>();
        game = diContainer.GetTag<Game>();
        savegame = diContainer.GetTag<zzio.Savegame>();
        recorder = diContainer.GetTag<EntityCommandRecorder>();
        startDialogDisposable = World.Subscribe<messages.StartDialog>(HandleStartDialog);
        removedDisposable = World.SubscribeEntityComponentRemoved<components.DialogState>(HandleDialogStateRemoved);
        sceneLoadedDisposable = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public override void Dispose()
    {
        base.Dispose();
        startDialogDisposable.Dispose();
        removedDisposable.Dispose();
        sceneLoadedDisposable.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded msg) => scene = msg.Scene;

    private void HandleStartDialog(in messages.StartDialog message)
    {
        if (dialogEntity.IsAlive)
            throw new InvalidOperationException("A dialog is already open");

        World.Publish(new messages.SpawnSample("resources/audio/sfx/gui/_g002.wav"));

        dialogEntity = World.CreateEntity();
        var dialogEntityRecord = RecordDialogEntity();
        dialogEntityRecord.Set(components.DialogState.NextScriptOp);
        dialogEntityRecord.Set(new components.ScriptExecution(GetScriptSource(message)));
        dialogEntityRecord.Set(new components.DialogNPC(message.NpcEntity));
        dialogEntityRecord.Set(new components.DialogCommonUI
        {
            Letterbox = CreateLetterbox(),
            SayLabel = CreateSayLabel()
        });

        var playerRecord = recorder.Record(game.PlayerEntity);
        playerRecord.Set(game.PlayerEntity.Get<components.NonFairyAnimation>() with { Next = AnimationType.Idle0 });

        World.Publish(default(messages.ui.GameScreenOpened));
        World.Publish(messages.LockPlayerControl.Forever);
    }

    private DefaultEcs.Entity CreateLetterbox()
    {
        var uiWorld = ui.GetTag<DefaultEcs.World>();
        var letterboxEntity = uiWorld.CreateEntity();
        letterboxEntity.Set(new components.Parent(dialogEntity));
        letterboxEntity.Set(components.Visibility.Visible);
        letterboxEntity.Set(new components.ui.RenderOrder(1));
        letterboxEntity.Set(components.ui.UIOffset.ScreenUpperLeft);
        letterboxEntity.Set(null as materials.UIMaterial); // untextured
        letterboxEntity.Set(IColor.Clear);
        letterboxEntity.Set(components.ui.Fade.StdIn);
        letterboxEntity.Set(new components.ui.Tile[]
        {
            new(-1, Rect.FromTopLeftSize(
                Vector2.Zero,
                ui.LogicalScreen.Size with { Y = UpperLetterboxHeight })),
            new(-1, Rect.FromTopLeftSize(
                Vector2.UnitY * (ui.LogicalScreen.Size.Y - LowerLetterboxHeight),
                ui.LogicalScreen.Size with { Y = LowerLetterboxHeight }))
        });
        return letterboxEntity;
    }

    private DefaultEcs.Entity CreateSayLabel() => ui.Preload.CreateLabel(dialogEntity)
        .With(new Vector2(25, ui.LogicalScreen.Size.Y - 90))
        .With(ui.Preload.Fnt003)
        .With(components.ui.UIOffset.ScreenUpperLeft);

    private void HandleDialogStateRemoved(in DefaultEcs.Entity _, in components.DialogState __)
    {
        World.Publish(default(messages.ui.GameScreenClosed));
        World.Publish(messages.LockPlayerControl.Unlock);
        World.Publish(messages.SetCameraMode.Overworld);
    }

    [WithPredicate]
    private bool ShouldContinueScript(in components.DialogState state) => state == components.DialogState.NextScriptOp;

    [Update]
    private void Update(in DefaultEcs.Entity entity, ref components.ScriptExecution execution)
    {
        if (!Continue(entity, ref execution))
            dialogEntity.Set(components.DialogState.FadeOut);
    }

    private static readonly FilePath VoiceBasePath = new("resources/audio/speech/");
    private void Say(DefaultEcs.Entity entity, UID uid, bool silent)
    {
        var sayLabel = entity.Get<components.DialogCommonUI>().SayLabel;
        var tileSheet = sayLabel.Get<rendering.TileSheet>();
        var textRow = db.GetDialog(uid);
        var text = tileSheet.WrapLines(textRow.Text, ui.LogicalScreen.Size.X - 60);

        sayLabel.Set(new components.ui.AnimatedLabel(text, SegmentsPerAddSay, isBlinking: !silent));

        if (silent)
            return;
        string? nextSample = textRow.Voice;
        if (string.IsNullOrWhiteSpace(nextSample))
        {
            if (NPCEntity.TryGet<components.ActorParts>(out var actorParts) &&
                actorParts.Body.TryGet<resources.ClumpInfo>(out var bodyClumpInfo))
                nextSample = $"{bodyClumpInfo.Name[..^4]}{Random.Next(1, 4)}.wav";
            else
                logger.Warning("Tried to play random voice for NPC without body ({Entity})", NPCEntity);
        }
        if (string.IsNullOrWhiteSpace(nextSample))
            return;
        var voiceSamplePath = VoiceBasePath.Combine(nextSample).ToPOSIXString();
        if (resourcePool.FindFile(voiceSamplePath) is null)
        {
            logger.Warning("Could not find voice sample {VoiceSample}", nextSample);
            return;
        }

        if (entity.TryGet<components.DialogVoiceSample>(out var voiceSample) &&
            voiceSample.Entity.IsAlive)
            voiceSample.Entity.Dispose();
        var voiceSampleEntity = ui.World.CreateEntity();
        voiceSampleEntity.Set(new components.Parent(entity));
        ui.World.Publish(new messages.SpawnSample(voiceSamplePath, AsEntity: voiceSampleEntity));
        entity.Set(new components.DialogVoiceSample(voiceSampleEntity));
    }

    private void Choice(DefaultEcs.Entity entity, int targetLabel, UID uid)
    {
        World.Publish(new messages.DialogAddChoice(entity, targetLabel, uid));
    }

    private void WaitForUser(DefaultEcs.Entity entity)
    {
        if (entity.TryGet<messages.DialogTalk>(out var talkMessage))
        {
            entity.Remove<messages.DialogTalk>();
            World.Publish(talkMessage);
        }
        else if (entity.TryGet<messages.DialogTrading>(out var tradingMessage))
        {
            entity.Remove<messages.DialogTrading>();
            World.Publish(tradingMessage);
        }
        else if (entity.TryGet<messages.DialogGambling>(out var gamblingMessage))
        {
            entity.Remove<messages.DialogGambling>();
            World.Publish(gamblingMessage);
        }
        else
            entity.Set(components.DialogState.WaitForSayString);
    }

    private void SetCamera(DefaultEcs.Entity entity, int cameraMode)
    {
        // TODO: Add NpcCamera for modes 2100-2105 and 2110-2115
        World.Publish(new messages.SetCameraMode(cameraMode, NPCEntity));
    }

    private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
    {
        World.Publish(new messages.NPCChangeWaypoint(NPCEntity, fromWpId, toWpId));
        NPCEntity.Set(new components.NPCIsWalkingByDialog(entity));
        entity.Set(components.DialogState.NpcWalking);
    }

    private void Fight(DefaultEcs.Entity entity, int stage, bool canFlee)
    {
        LogUnimplementedInstructionWarning();
    }

    private void ChangeDatabase(DefaultEcs.Entity entity, UID uid)
    {
        var npcEntity = NPCEntity;
        var dbRow = db.GetNpc(uid);
        World.Publish(new GSModChangeNPCState(npcEntity.Get<Trigger>().idx, uid));
        npcEntity.Set(dbRow);
        if (dbRow.InitScript.Length > 0)
        {
            npcEntity.Set(new components.ScriptExecution(dbRow.InitScript));
            World.Publish(new messages.ExecuteNPCScript(OnlyFor: npcEntity));
        }
        if (dbRow.UpdateScript.Length > 0)
            npcEntity.Set(new components.ScriptExecution(dbRow.UpdateScript));
        else
            npcEntity.Remove<components.ScriptExecution>();
    }

    private void RemoveNpc(DefaultEcs.Entity entity)
    {
        var npcEntity = NPCEntity;
        npcEntity.Set<components.Dead>();
        var npcTrigger = npcEntity.Get<Trigger>();
        if (npcTrigger.type != TriggerType.NpcAttackPosition || npcTrigger.ii3 == 100)
            World.Publish(new GSModDisableTrigger(npcTrigger.idx));
        else
        {
            var triggerEntity = World.GetEntities()
                .With((in Trigger t) => t == npcTrigger)
                .AsEnumerable()
                .FirstOrDefault();
            if (triggerEntity.IsAlive)
                triggerEntity.Set<components.Disabled>();
        }

        ref var playerPuppet = ref game.PlayerEntity.Get<components.PuppetActorMovement>();
        playerPuppet.TargetDirection = MathEx.SafeNormalize(playerPuppet.TargetDirection with { Y = 0f });

        entity.Set(components.DialogState.FadeOut);
    }

    private void CatchWizform(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }

    private void KillPlayer(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }

    private static void TradingCurrency(DefaultEcs.Entity entity, UID uid)
    {
        entity.Set(new messages.DialogTrading(entity, uid, []));
    }

    private static void TradingCard(DefaultEcs.Entity entity, int price, UID uid)
    {
        entity.Get<messages.DialogTrading>().CardTrades.Add((price, uid));
    }

    private static void SetupGambling(DefaultEcs.Entity entity, int count, int type, int id)
    {
        if (!entity.TryGet<messages.DialogGambling>(out var gamblingMessage)){
            entity.Set(new messages.DialogGambling(entity, []));
        }
        for (int i = 0; i < count; i++)
            entity.Get<messages.DialogGambling>().Cards.Add(type == 1 ? id : null);
    }

    private bool IfTriggerIsActive(DefaultEcs.Entity entity, int triggerI)
    {
        LogUnimplementedInstructionWarning();
        return false;
    }

    private void LockUserInput(DefaultEcs.Entity entity, int mode)
    {
        // TODO: Dialog LockUserInput is missing mode 2 behaviour
        if (mode is 1 or 2)
        {
            World.Publish(messages.LockPlayerControl.Forever);
            World.Publish(new messages.ResetPlayerMovement());
        }
        else
            World.Publish(messages.LockPlayerControl.Unlock);
    }

    private void ModifyTrigger(DefaultEcs.Entity entity, int enableTrigger, int id, int triggerI)
    {
        LogUnimplementedInstructionWarning();
    }

    private void NpcWizformEscapes(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }

    private static void Talk(DefaultEcs.Entity entity, UID uid)
    {
        // do not publish directly to let two sequential Talk commands override each other
        entity.Set(new messages.DialogTalk(entity, uid));
    }

    private static void SetTalkLabels(DefaultEcs.Entity entity, int labelYes, int labelNo, TalkMode mode)
    {
        entity.Set(mode switch
        {
            TalkMode.Exit => components.DialogTalkLabels.Exit,
            TalkMode.Continue => components.DialogTalkLabels.Continue,
            TalkMode.YesNo => components.DialogTalkLabels.YesNo(labelYes, labelNo),
            _ => throw new NotSupportedException($"Unsupported talk mode: {mode}")
        });
    }

    private void ChafferWizforms(UID uid, UID uid2, UID uid3)
    {
        LogUnimplementedInstructionWarning();
    }

    private void DeployMeAtTrigger(int triggerI)
    {
        World.Publish(new messages.CreaturePlaceToTrigger(NPCEntity, triggerI, orientByTrigger: true, moveToGround: true));
        NPCEntity.Get<components.NPCMovement>().CurWaypointId = -1;
    }

    private void DeployPlayerAtTrigger(int triggerI)
    {
        World.Publish(new messages.CreaturePlaceToTrigger(game.PlayerEntity, triggerI, orientByTrigger: true, moveToGround: true));
    }

    private void DeployNPCAtTrigger(int triggerI, UID uid)
    {
        var otherNpc = World.GetEntities()
            .With((in zzio.db.NpcRow dbRow) => dbRow.Uid == uid)
            .AsEnumerable()
            .FirstOrDefault();
        if (!otherNpc.IsAlive)
            return;

        otherNpc.Get<components.NPCMovement>().CurWaypointId = -1;
        var isFairyNpc = otherNpc.Get<components.NPCType>() == components.NPCType.Flying;
        World.Publish(new messages.CreaturePlaceToTrigger(game.PlayerEntity, triggerI, orientByTrigger: true, moveToGround: !isFairyNpc));

        if (isFairyNpc)
            logger.Warning("DeployNPCAtTrigger not implemented for fairy NPCs"); // TODO: Implement DeployNPCAtTrigger for fairy NPCs
    }

    private void Delay(DefaultEcs.Entity entity, int duration)
    {
        var dialogRecord = recorder.Record(entity);
        dialogRecord.Set(new components.DialogDelay(duration * 0.1f));
        dialogRecord.Set(components.DialogState.Delay);
    }

    private bool IfNPCModifierHasValue(DefaultEcs.Entity entity, int value)
    {
        return NPCEntity.Get<components.NPCModifier>().Value == value;
    }

    private void SetNPCModifier(DefaultEcs.Entity entity, int scene, int optTriggerI, int value)
    {
        uint triggerI = optTriggerI < 0
            ? NPCEntity.Get<Trigger>().idx
            : (uint)optTriggerI;
        var gsmod = new GSModSetNPCModifier(triggerI, value);

        if (scene < 0)
            World.Publish(gsmod);
        else
            World.Publish(new messages.GSModForScene(scene, gsmod));
    }

    private bool IfPlayerIsClose(DefaultEcs.Entity entity, int maxDistSqr)
    {
        LogUnimplementedInstructionWarning();
        return false;
    }

    private bool IfNumberOfNpcsIs(DefaultEcs.Entity entity, int count, UID uid)
    {
        LogUnimplementedInstructionWarning();
        return false;
    }

    private void StartEffect(DefaultEcs.Entity entity, int effectType, int triggerI)
    {
        LogUnimplementedInstructionWarning();
    }

    private void TradeWizform(DefaultEcs.Entity entity, int id)
    {
        LogUnimplementedInstructionWarning();
    }

    private void CreateDynamicItems(DefaultEcs.Entity entity, int id, int count, int triggerI)
    {
        if (triggerI >= scene.triggers.Length)
            throw new ArgumentOutOfRangeException(nameof(triggerI), $"Invalid trigger index for CreateDynamicItems");
        var position = triggerI < 0
            ? NPCEntity.Get<Location>().LocalPosition
            : scene.triggers[triggerI].pos;
        World.Publish(new messages.CreateItem(id, position, count));
    }

    private void PlayVideo(DefaultEcs.Entity entity, int id)
    {
        LogUnimplementedInstructionWarning();
    }

    private void RemoveNpcAtTrigger(int triggerI)
    {
        LogUnimplementedInstructionWarning();
    }

    private bool IfTriggerIsEnabled(int triggerI)
    {
        LogUnimplementedInstructionWarning();
        return false;
    }

    private static readonly IReadOnlyList<string> SoundSamples =
    [
        "resources/audio/sfx/specials/_s022.wav",
        "resources/audio/sfx/specials/_s029.wav",
        "resources/audio/sfx/specials/_s030.wav",
        "resources/audio/sfx/specials/_s032.wav",
        "resources/audio/sfx/specials/_s021.wav",
        "resources/audio/sfx/specials/_s023.wav"
    ];
    private void PlaySound(DefaultEcs.Entity entity, int id)
    {
        if (id < 0 || id >= SoundSamples.Count)
            logger.Error("PlaySound instruction with invalid sample ID {ID}", id);
        else
            World.Publish(new messages.SpawnSample(SoundSamples[id]));
    }

    private void PlayInArena(DefaultEcs.Entity entity, int arg)
    {
        LogUnimplementedInstructionWarning();
    }

    private void EndActorEffect(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }

    private void CreateSceneObjects(DefaultEcs.Entity entity, SceneObjectType objectType)
    {
        LogUnimplementedInstructionWarning();
    }

    private void RemoveBehavior(DefaultEcs.Entity entity, int id)
    {
        LogUnimplementedInstructionWarning();
    }

    private void UnlockDoor(DefaultEcs.Entity entity, int id, bool isMetalDoor)
    {
        LogUnimplementedInstructionWarning();
    }

    private void EndGame(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }

    private void SubGame(DefaultEcs.Entity entity, SubGameType subGameType, int size, int labelExit)
    {
        LogUnimplementedInstructionWarning();
    }

    private void PlayAnimation(DefaultEcs.Entity entity, AnimationType animation)
    {
        var body = NPCEntity.Get<components.ActorParts>().Body;
        if (body.Get<components.AnimationPool>().Contains(animation))
            NPCEntity.Get<components.NonFairyAnimation>().Next = animation;
        else
            logger.Warning("Dialog script tried to play missing animation: " + animation);
    }

    private void PlayPlayerAnimation(DefaultEcs.Entity entity, AnimationType animation)
    {
        game.PlayerEntity.Get<components.NonFairyAnimation>().Next = animation;
    }

    private static readonly FilePath AmyVoiceBasePath = new("resources/audio/sfx/voices/amy/");
    private void PlayAmyVoice(string v)
    {
        var fullPath = AmyVoiceBasePath.Combine(v + ".wav");
        World.Publish(new messages.SpawnSample(fullPath.ToPOSIXString()));
    }

    private void CreateDynamicModel(DefaultEcs.Entity entity)
    {
        LogUnimplementedInstructionWarning();
    }

    private void DeploySound(DefaultEcs.Entity entity, int id, int triggerI)
    {
        // unused
        LogUnimplementedInstructionWarning();
    }
}
