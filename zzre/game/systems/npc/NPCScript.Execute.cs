using System;
using System.Linq;
using zzio;
using zzio.script;

namespace zzre.game.systems;

partial class NPCScript
{
    private const string CmdSetModel = "C";
    private const string CmdSetCamera = "&";
    private const string CmdWizform = "'";
    private const string CmdSpell = "(";
    private const string CmdChangeWaypoint = ")";
    private const string CmdLookAtPlayer = "+";
    private const string CmdRemoveNPC = "-";
    private const string CmdIfTriggerIsActive = "=";
    private const string CmdMoveSystem = ":";
    private const string CmdMovementSpeed = "?";
    private const string CmdLockUserInput = "<";
    private const string CmdPlayAnimation = "A";
    private const string CmdStartPrelude = "E";
    private const string CmdSetNPCType = "O";
    private const string CmdDeployNPCAtTrigger = "P";
    private const string CmdIfCloseToWaypoint = "S";
    private const string CmdIfNPCModifierHasValue = "U";
    private const string CmdSetNPCModifier = "V";
    private const string CmdDefaultWizform = "W";
    private const string CmdDefaultDeck = "n";
    private const string CmdIdle = "X";
    private const string CmdIfPlayerIsClose = "Y";
    private const string CmdSetCollision = "]";
    private const string CmdCreateDynamicItems = "_";
    private const string CmdRevive = "b";
    private const string CmdLookAtTrigger = "c";
    private const string CmdPlaySound = "e";

    protected override OpReturn Execute(in DefaultEcs.Entity entity, ref components.ScriptExecution script, RawInstruction instruction)
    {
        var args = instruction.Arguments;
        switch (instruction.Command)
        {
            case CmdSetModel:
                SetModel(entity, args.Single());
                return OpReturn.Continue;

            case CmdSetCamera:
                var triggerArg = int.Parse(args.Single());
                SetCamera(entity, triggerArg);
                return OpReturn.Continue;

            case CmdWizform:
                var atIndex = int.Parse(args[0]);
                var fairyId = int.Parse(args[1]);
                var level = int.Parse(args[2]);
                Wizform(entity, atIndex, fairyId, level);
                return OpReturn.Continue;

            case CmdSpell:
                var fairyI = int.Parse(args[0]);
                var slotI = int.Parse(args[1]);
                var spellId = int.Parse(args[2]);
                Spell(entity, fairyI, slotI, spellId);
                return OpReturn.Continue;

            case CmdChangeWaypoint:
                var fromWpId = int.Parse(args[0]);
                var toWpId = int.Parse(args[1]);
                ChangeWaypoint(entity, fromWpId, toWpId);
                return OpReturn.Pause;

            case CmdLookAtPlayer:
                var duration = int.Parse(args[0]);
                var rotationMode = Enum.Parse<components.NPCLookAtPlayer.Mode>(args[1]);
                LookAtPlayer(entity, duration, rotationMode);
                return OpReturn.Pause;

            case CmdRemoveNPC:
                RemoveNPC(entity);
                return OpReturn.Continue;

            case CmdIfTriggerIsActive:
                var triggerI = int.Parse(args.Single());
                return IfTriggerIsActive(entity, triggerI)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdMoveSystem:
                var waypointMode = Enum.Parse<messages.NPCMoveSystem.Mode>(args[0]);
                var wpCategory = int.Parse(args[1]);
                MoveSystem(entity, waypointMode, wpCategory);
                return OpReturn.Pause;

            case CmdMovementSpeed:
                var speed = int.Parse(args.Single());
                MovementSpeed(entity, speed);
                return OpReturn.Continue;

            case CmdLockUserInput:
                var isLocked = int.Parse(args.Single()) == 1;
                LockUserInput(entity, isLocked);
                return OpReturn.Continue;

            case CmdPlayAnimation:
                var animationType = Enum.Parse<zzio.AnimationType>(args[0]);
                duration = int.Parse(args[1]);
                PlayAnimation(entity, animationType, duration);
                return duration <= 0
                    ? OpReturn.Continue
                    : OpReturn.Pause;

            case CmdStartPrelude:
                StartPrelude(entity);
                return OpReturn.Pause;

            case CmdSetNPCType:
                var type = Enum.Parse<components.NPCType>(args.Single());
                SetNPCType(entity, type);
                return OpReturn.Continue;

            case CmdDeployNPCAtTrigger:
                triggerI = int.Parse(args[0]);
                switch (args[1])
                {
                    case "0": DeployMeAtTrigger(entity, triggerI); break;
                    case "1": DeployPlayerAtTrigger(entity, triggerI); break;
                    default:
                        var uid = UID.Parse(args[1]);
                        DeployNPCAtTrigger(entity, uid);
                        break;
                }
                return OpReturn.Continue;

            case CmdIfCloseToWaypoint:
                var waypointI = int.Parse(args.Single());
                return IfCloseToWaypoint(entity, waypointI)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdIfNPCModifierHasValue:
                var value = int.Parse(args.Single());
                return IfNPCModifierHasValue(entity, value)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdSetNPCModifier:
                var scene = int.Parse(args[0]);
                triggerI = int.Parse(args[1]);
                value = int.Parse(args[2]);
                SetNPCModifier(entity, scene, triggerI, value);
                return OpReturn.Continue;

            case CmdDefaultWizform:
                fairyId = int.Parse(args[0]);
                var groupOrSlotI = int.Parse(args[1]);
                level = int.Parse(args[2]);
                DefaultWizform(entity, fairyId, groupOrSlotI, level);
                return OpReturn.Continue;

            case CmdDefaultDeck:
                var groupI = int.Parse(args[0]);
                level = int.Parse(args[1]);
                // unused third parameter
                DefaultDeck(entity, groupI, level);
                return OpReturn.Continue;

            case CmdIdle:
                Idle(entity);
                return OpReturn.Pause;

            case CmdIfPlayerIsClose:
                var maxDistSqr = int.Parse(args.Single());
                return IfPlayerIsClose(entity, maxDistSqr)
                    ? OpReturn.Continue
                    : OpReturn.ConditionalSkip;

            case CmdSetCollision:
                var isSolid = int.Parse(args.Single()) != 0;
                SetCollision(entity, isSolid);
                return OpReturn.Continue;

            case CmdCreateDynamicItems:
                var itemId = int.Parse(args[0]);
                var count = int.Parse(args[1]);
                triggerI = int.Parse(args[2]);
                CreateDynamicItems(entity, itemId, count, triggerI);
                return OpReturn.Continue;

            case CmdRevive:
                Revive(entity);
                return OpReturn.Continue;

            case CmdLookAtTrigger:
                duration = int.Parse(args[0]);
                triggerI = int.Parse(args[1]);
                LookAtTrigger(entity, duration, triggerI);
                return OpReturn.Continue;

            case CmdPlaySound:
                var id = int.Parse(args[0]);
                PlaySound(entity, id);
                return OpReturn.Continue;

            default: return OpReturn.UnknownInstruction;
        }
    }
}
