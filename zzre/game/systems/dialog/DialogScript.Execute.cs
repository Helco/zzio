using System;
using System.Linq;
using zzio;
using zzio.script;

namespace zzre.game.systems;

partial class DialogScript
{
    private const string CmdSay = "!";
    private const string CmdChoice = "\"";
    private const string CmdWaitForUser = "#";
    private const string CmdSetCamera = "&";
    private const string CmdChangeWaypoint = ")";
    private const string CmdFight = "*";
    private const string CmdChangeDatabase = ",";
    private const string CmdRemoveNpc = "-";
    private const string CmdCatchWizform = ".";
    private const string CmdKillPlayer = "0";
    private const string CmdTradingCurrency = "5";
    private const string CmdTradingItem = "2";
    private const string CmdTradingSpell = "3";
    private const string CmdTradingWizform = "4";
    private const string CmdGivePlayerCards = "1";
    private const string CmdSetupGambling = "B";
    private const string CmdIfPlayerHasCards = "6";
    private const string CmdIfPlayerHasSpecials = "@";
    private const string CmdIfTriggerIsActive = "=";
    private const string CmdRemovePlayerCards = "9";
    private const string CmdLockUserInput = "<";
    private const string CmdModifyTrigger = ">";
    private const string CmdPlayAnimation = "A";
    private const string CmdNpcWizformEscapes = "F";
    private const string CmdTalk = "J";
    private const string CmdChafferWizforms = "N";
    private const string CmdDeployNpcAtTrigger = "P";
    private const string CmdDelay = "Q";
    private const string CmdRemoveWizforms = "T";
    private const string CmdIfNpcModifierHasValue = "U";
    private const string CmdSetNpcModifier = "V";
    private const string CmdIfPlayerIsClose = "Y";
    private const string CmdIfNumberOfNpcsIs = "Z";
    private const string CmdStartEffect = "[";
    private const string CmdSetTalkLabels = "\\";
    private const string CmdTradeWizform = "^";
    private const string CmdCreateDynamicItems = "_";
    private const string CmdPlayVideo = "`";
    private const string CmdRemoveNpcAtTrigger = "a";
    private const string CmdRevive = "b";
    private const string CmdIfTriggerIsEnabled = "d";
    private const string CmdPlaySound = "e";
    private const string CmdPlayInArena = "f";
    private const string CmdEndActorEffect = "h"; // this is no mistake, there is only endActorEffect
    private const string CmdCreateSceneObjects = "i";
    private const string CmdRemoveBehavior = "k";
    private const string CmdUnlockDoor = "l";
    private const string CmdEndGame = "m";
    private const string CmdSubGame = "o";
    private const string CmdModifyEffect = "p";
    private const string CmdPlayPlayerAnimation = "q";
    private const string CmdPlayAmyVoice = "s";
    private const string CmdCreateDynamicModel = "r";
    private const string CmdDeploySound = "t";
    private const string CmdGivePlayerPresent = "u";

    protected override OpReturn Execute(in DefaultEcs.Entity entity, ref components.ScriptExecution script, RawInstruction instruction)
    {
        var args = instruction.Arguments;
        switch (instruction.Command)
        {
            case CmdSay:
                var uid = UID.Parse(args[0]);
                var silent = int.Parse(args[1]) != 0;
                Say(entity, uid, silent);
                return OpReturn.Continue;

            case CmdChoice:
                var targetLabel = int.Parse(args[0]);
                uid = UID.Parse(args[1]);
                Choice(entity, targetLabel, uid);
                return OpReturn.Continue;

            case CmdWaitForUser:
                WaitForUser(entity);
                return OpReturn.Pause;

            case CmdSetCamera:
                var triggerArg = int.Parse(args.Single());
                SetCamera(entity, triggerArg);
                return OpReturn.Continue;

            case CmdChangeWaypoint:
                var fromWpId = int.Parse(args[0]);
                var toWpId = int.Parse(args[1]);
                ChangeWaypoint(entity, fromWpId, toWpId);
                return OpReturn.Pause;

            case CmdFight:
                var stage = int.Parse(args[0]);
                var canFlee = int.Parse(args[1]) != 0;
                Fight(entity, stage, canFlee);
                return OpReturn.Stop;

            case CmdChangeDatabase:
                uid = UID.Parse(args[0]);
                ChangeDatabase(entity, uid);
                return OpReturn.Continue;

            case CmdRemoveNpc:
                RemoveNpc(entity);
                return OpReturn.Stop;

            case CmdCatchWizform:
                CatchWizform(entity);
                return OpReturn.Pause;

            case CmdKillPlayer:
                KillPlayer(entity);
                return OpReturn.Continue; // surprisingly not pause/stop

            case CmdTradingCurrency:
                uid = UID.Parse(args[0]);
                TradingCurrency(entity, uid);
                return OpReturn.Continue;

            case CmdTradingItem:
            case CmdTradingSpell:
            case CmdTradingWizform:
                var price = int.Parse(args[0]);
                uid = UID.Parse(args[1]);
                TradingCard(entity, price, uid);
                return OpReturn.Continue;

            case CmdGivePlayerCards:
                var count = int.Parse(args[0]);
                var cardType = Enum.Parse<CardType>(args[1]);
                var id = int.Parse(args[2]);
                GivePlayerCards(entity, count, cardType, id);
                return OpReturn.Continue; // TODO: Confirm givePlayerCards, there could be a dialog happening

            case CmdSetupGambling:
                count = int.Parse(args[0]);
                var type = int.Parse(args[1]);
                id = int.Parse(args[2]);
                SetupGambling(entity, count, type, id);
                return OpReturn.Continue;

            case CmdIfPlayerHasCards:
                count = int.Parse(args[0]);
                cardType = Enum.Parse<CardType>(args[1]);
                id = int.Parse(args[2]);
                return IfPlayerHasCards(entity, count, cardType, id)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdIfPlayerHasSpecials:
                var specialType = Enum.Parse<SpecialInventoryCheck>(args[0]);
                var arg = int.Parse(args[1]);
                return IfPlayerHasSpecials(entity, specialType, arg)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdIfTriggerIsActive:
                var triggerI = int.Parse(args.Single());
                return IfTriggerIsActive(entity, triggerI)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdRemovePlayerCards:
                count = int.Parse(args[0]);
                cardType = Enum.Parse<CardType>(args[1]);
                id = int.Parse(args[2]);
                RemovePlayerCards(entity, count, cardType, id);
                return OpReturn.Continue;

            case CmdLockUserInput:
                var mode = int.Parse(args[0]);
                LockUserInput(entity, mode);
                return OpReturn.Continue;

            case CmdModifyTrigger:
                var enableTrigger = int.Parse(args[0]);
                id = int.Parse(args[1]);
                triggerI = int.Parse(args[2]);
                ModifyTrigger(entity, enableTrigger, id, triggerI);
                return OpReturn.Continue;

            case CmdPlayAnimation:
                var animation = Enum.Parse<AnimationType>(args[0]);
                // there is a second, unused argument
                PlayAnimation(entity, animation);
                return OpReturn.Continue;

            case CmdNpcWizformEscapes:
                NpcWizformEscapes(entity);
                return OpReturn.Pause;

            case CmdTalk:
                uid = UID.Parse(args[0]);
                // there is a second, unused argument
                Talk(entity, uid);
                return OpReturn.Pause;

            case CmdChafferWizforms:
                uid = UID.Parse(args[0]);
                var uid2 = UID.Parse(args[1]);
                var uid3 = UID.Parse(args[2]);
                ChafferWizforms(uid, uid2, uid3);
                return OpReturn.Pause;

            case CmdDeployNpcAtTrigger:
                triggerI = int.Parse(args[0]);
                switch (args[1])
                {
                    case "0": DeployMeAtTrigger(triggerI); break;
                    case "1": DeployPlayerAtTrigger(triggerI); break;
                    default:
                        uid = UID.Parse(args[1]);
                        DeployNPCAtTrigger(triggerI, uid);
                        break;
                }
                return OpReturn.Continue;

            case CmdDelay:
                var duration = int.Parse(args[0]);
                Delay(entity, duration);
                return OpReturn.Pause;

            case CmdRemoveWizforms:
                RemoveWizforms(entity);
                return OpReturn.Continue;

            case CmdIfNpcModifierHasValue:
                var value = int.Parse(args.Single());
                return IfNPCModifierHasValue(entity, value)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdSetNpcModifier:
                var scene = int.Parse(args[0]);
                triggerI = int.Parse(args[1]);
                value = int.Parse(args[2]);
                SetNPCModifier(entity, scene, triggerI, value);
                return OpReturn.Continue;

            case CmdIfPlayerIsClose:
                var maxDistSqr = int.Parse(args.Single());
                return IfPlayerIsClose(entity, maxDistSqr)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdIfNumberOfNpcsIs:
                count = int.Parse(args[0]);
                uid = UID.Parse(args[1]);
                return IfNumberOfNpcsIs(entity, count, uid)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdStartEffect:
                var effectType = int.Parse(args[0]);
                triggerI = int.Parse(args[1]);
                StartEffect(entity, effectType, triggerI);
                return OpReturn.Continue;

            case CmdSetTalkLabels:
                var labelYes = int.Parse(args[0]);
                var labelNo = int.Parse(args[1]);
                var talkMode = Enum.Parse<TalkMode>(args[2]);
                SetTalkLabels(entity, labelYes, labelNo, talkMode);
                return OpReturn.Continue;

            case CmdTradeWizform:
                id = int.Parse(args[0]);
                TradeWizform(entity, id);
                return OpReturn.Continue;

            case CmdCreateDynamicItems:
                id = int.Parse(args[0]);
                count = int.Parse(args[1]);
                triggerI = int.Parse(args[2]);
                CreateDynamicItems(entity, id, count, triggerI);
                return OpReturn.Continue;

            case CmdPlayVideo:
                id = int.Parse(args[0]);
                PlayVideo(entity, id);
                return OpReturn.Pause;

            case CmdRemoveNpcAtTrigger:
                triggerI = int.Parse(args[0]);
                RemoveNpcAtTrigger(triggerI);
                return OpReturn.Continue;

            case CmdRevive:
                Revive(entity);
                return OpReturn.Continue;

            case CmdIfTriggerIsEnabled:
                triggerI = int.Parse(args[0]);
                return IfTriggerIsEnabled(triggerI)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdPlaySound:
                id = int.Parse(args[0]);
                PlaySound(entity, id);
                return OpReturn.Continue;

            case CmdPlayInArena:
                arg = int.Parse(args[0]);
                // There is a second, unused argument
                PlayInArena(entity, arg);
                return OpReturn.Pause;

            case CmdEndActorEffect:
                EndActorEffect(entity);
                return OpReturn.Continue;

            case CmdCreateSceneObjects:
                var objectType = Enum.Parse<SceneObjectType>(args[0]);
                CreateSceneObjects(entity, objectType);
                return OpReturn.Continue;

            case CmdRemoveBehavior:
                id = int.Parse(args[0]);
                RemoveBehavior(entity, id);
                return OpReturn.Continue;

            case CmdUnlockDoor:
                id = int.Parse(args[0]);
                var isMetalDoor = int.Parse(args[1]) != 0;
                UnlockDoor(entity, id, isMetalDoor);
                return OpReturn.Continue;

            case CmdEndGame:
                EndGame(entity);
                return OpReturn.Continue;

            case CmdSubGame:
                var subGameType = Enum.Parse<SubGameType>(args[0]);
                var size = int.Parse(args[1]);
                var labelExit = int.Parse(args[2]);
                SubGame(entity, subGameType, size, labelExit);
                return OpReturn.Pause;

            case CmdModifyEffect:
                // three arguments, but this is a noop
                return OpReturn.Continue;

            case CmdPlayPlayerAnimation:
                animation = Enum.Parse<AnimationType>(args[0]);
                // there is a second, unused argument
                PlayPlayerAnimation(entity, animation);
                return OpReturn.Continue;

            case CmdPlayAmyVoice:
                PlayAmyVoice(args[0]); // a rare string argument
                return OpReturn.Continue;

            case CmdCreateDynamicModel:
                // three unused arguments
                CreateDynamicModel(entity);
                return OpReturn.Continue;

            case CmdDeploySound:
                id = int.Parse(args[0]);
                triggerI = int.Parse(args[1]);
                DeploySound(entity, id, triggerI);
                return OpReturn.Continue;

            case CmdGivePlayerPresent:
                // one unused argument
                GivePlayerPresent(entity);
                return OpReturn.Continue;

            default: return OpReturn.UnknownInstruction;
        }
    }
}
