using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.Command;
using DefaultEcs.System;
using zzio;
using zzio.scn;
using zzio.db;

namespace zzre.game.systems
{
    public partial class DialogScript : BaseScript
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

        private readonly MappedDB db;
        private readonly UI ui;
        private readonly Scene scene;
        private readonly Game game;
        private readonly zzio.Savegame savegame;
        private readonly EntityCommandRecorder recorder;
        private readonly IDisposable startDialogDisposable;
        private readonly IDisposable removedDisposable;

        private DefaultEcs.Entity dialogEntity;
        private EntityRecord RecordDialogEntity() => recorder.Record(dialogEntity);
        private DefaultEcs.Entity NPCEntity => dialogEntity.Get<components.DialogNPC>().Entity;

        public DialogScript(ITagContainer diContainer) : base(diContainer, CreateEntityContainer)
        {
            World.SetMaxCapacity<components.DialogState>(1);
            db = diContainer.GetTag<MappedDB>();
            ui = diContainer.GetTag<UI>();
            scene = diContainer.GetTag<Scene>();
            game = diContainer.GetTag<Game>();
            savegame = diContainer.GetTag<zzio.Savegame>();
            recorder = diContainer.GetTag<EntityCommandRecorder>();
            startDialogDisposable = World.Subscribe<messages.StartDialog>(HandleStartDialog);
            removedDisposable = World.SubscribeComponentRemoved<components.DialogState>(HandleDialogStateRemoved);
        }

        public override void Dispose()
        {
            base.Dispose();
            startDialogDisposable.Dispose();
        }

        private void HandleStartDialog(in messages.StartDialog message)
        {
            if (dialogEntity.IsAlive)
                throw new InvalidOperationException("A dialog is already open");

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
            letterboxEntity.Set(null as materials.UIMaterial);
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

        private DefaultEcs.Entity CreateSayLabel()
        {
            return ui.Preload.CreateLabel(
                dialogEntity,
                new Vector2(25, ui.LogicalScreen.Size.Y - 90),
                text: "",
                ui.Preload.Fnt003,
                offset: components.ui.UIOffset.ScreenUpperLeft);
        }

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

        private void Say(DefaultEcs.Entity entity, UID uid, bool silent)
        {
            var sayLabel = entity.Get<components.DialogCommonUI>().SayLabel;
            var tileSheet = sayLabel.Get<rendering.TileSheet>();
            var text = db.GetDialog(uid).Text;
            text = tileSheet.WrapLines(text, ui.LogicalScreen.Size.X - 60);
            sayLabel.Set(new components.ui.AnimatedLabel(text, SegmentsPerAddSay, isBlinking: !silent));

            // TODO: Play voice sample on say instruction
        }

        private void Choice(DefaultEcs.Entity entity, int targetLabel, UID uid)
        {
            World.Publish(new messages.DialogAddChoice(entity, targetLabel, uid));
        }

        private void WaitForUser(DefaultEcs.Entity entity)
        {
            dialogEntity.Set(components.DialogState.WaitForSayString);
        }

        private void SetCamera(DefaultEcs.Entity entity, int cameraMode)
        {
            // TODO: Add NpcCamera for modes 2100-2105 and 2110-2115
            World.Publish(new messages.SetCameraMode(cameraMode, NPCEntity));
        }

        private void ChangeWaypoint(DefaultEcs.Entity entity, int fromWpId, int toWpId)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void Fight(DefaultEcs.Entity entity, int stage, bool canFlee)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void ChangeDatabase(DefaultEcs.Entity entity, UID uid)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
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
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void KillPlayer(DefaultEcs.Entity entity)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void TradingCurrency(DefaultEcs.Entity entity, UID uid)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void TradingCard(DefaultEcs.Entity entity, int price, UID uid)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void SetupGambling(DefaultEcs.Entity entity, int count, int type, int id)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private bool IfTriggerIsActive(DefaultEcs.Entity entity, int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
            return false;
        }

        private void LockUserInput(DefaultEcs.Entity entity, int mode)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void ModifyTrigger(DefaultEcs.Entity entity, int enableTrigger, int id, int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void NpcWizformEscapes(DefaultEcs.Entity entity)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void Talk(DefaultEcs.Entity entity, UID uid)
        {
            World.Publish(new messages.DialogTalk(entity, uid));
        }

        private void SetTalkLabels(DefaultEcs.Entity entity, int labelYes, int labelNo, TalkMode mode)
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
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
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
                Console.WriteLine("Warning: DeployNPCAtTrigger not implemented for fairy NPCs"); // TODO: Implement DeployNPCAtTrigger for fairy NPCs
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
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
            return false;
        }

        private bool IfNumberOfNpcsIs(DefaultEcs.Entity entity, int count, UID uid)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
            return false;
        }

        private void StartEffect(DefaultEcs.Entity entity, int effectType, int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void TradeWizform(DefaultEcs.Entity entity, int id)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void CreateDynamicItems(DefaultEcs.Entity entity, int id, int count, int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void PlayVideo(DefaultEcs.Entity entity, int id)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void RemoveNpcAtTrigger(int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private bool IfTriggerIsEnabled(int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
            return false;
        }

        private void PlaySound(DefaultEcs.Entity entity, int id)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void PlayInArena(DefaultEcs.Entity entity, int arg)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void EndActorEffect(DefaultEcs.Entity entity)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void CreateSceneObjects(DefaultEcs.Entity entity, SceneObjectType objectType)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void RemoveBehavior(DefaultEcs.Entity entity, int id)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void UnlockDoor(DefaultEcs.Entity entity, int id, bool isMetalDoor)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void EndGame(DefaultEcs.Entity entity)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void SubGame(DefaultEcs.Entity entity, SubGameType subGameType, int size, int labelExit)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void PlayAnimation(DefaultEcs.Entity entity, AnimationType animation)
        {
            NPCEntity.Get<components.NonFairyAnimation>().Next = animation;
        }

        private void PlayPlayerAnimation(DefaultEcs.Entity entity, AnimationType animation)
        {
            game.PlayerEntity.Get<components.NonFairyAnimation>().Next = animation;
        }

        private void PlayAmyVoice(string v)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void CreateDynamicModel(DefaultEcs.Entity entity)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }

        private void DeploySound(DefaultEcs.Entity entity, int id, int triggerI)
        {
            var curMethod = System.Reflection.MethodBase.GetCurrentMethod();
            Console.WriteLine($"Warning: unimplemented dialog instruction \"{curMethod!.Name}\"");
        }
    }
}
