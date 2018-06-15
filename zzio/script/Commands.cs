using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zzio.script
{
    enum ArgType
    {
        TextUID,
        NpcUID,
        DialogUID,
        ItemUID,
        SpellUID,
        FairyUID,
        FairyId,
        SpellId,
        ItemId,

        CameraPos,
        ModifyWizform,

        None
    }

    struct Command
    {
        public char shortName;
        public string longName;
        public uint maxArgs;
        public bool isBranchCmd;
        public ArgType[] argTypes;

        public bool isValid { get { return shortName != 0; } }

        public Command(char shortName, string longName, uint maxArgs, bool isBranchCmd = false, ArgType[] argTypes = null)
        {
            this.shortName = shortName;
            this.longName = longName;
            this.maxArgs = maxArgs;
            this.isBranchCmd = isBranchCmd;
            this.argTypes = argTypes;
        }

        public static Command[] commands =
        {
            new Command('!', "say", 2, false, new ArgType[] { ArgType.DialogUID }),
            new Command('C', "setModel", 1),
            new Command('"', "choice", 2, false, new ArgType[] { ArgType.None, ArgType.DialogUID }),
            new Command('#', "waitForUser", 0),
            new Command('$', "label", 1),
            new Command('&', "setCamera", 1, false, new ArgType[] { ArgType.CameraPos }),
            new Command('%', "exit", 0),
            new Command('\'', "wizform", 3),
            new Command('(', "spell", 3),
            new Command('8', "else", 0, true),
            new Command(')', "changeWaypoint", 2),
            new Command('*', "fight", 2),
            new Command('+', "lookAtPlayer", 2),
            new Command(',', "changeDatabase", 1, false, new ArgType[] { ArgType.NpcUID }),
            new Command('-', "removeNpc", 0),
            new Command('.', "catchWizform", 0),
            new Command('0', "killPlayer", 0),
            new Command('5', "tradingCurrency", 1, false, new ArgType[] { ArgType.ItemUID }),
            new Command('2', "tradingItem", 2, false, new ArgType[] { ArgType.None, ArgType.ItemUID }),
            new Command('3', "tradingSpell", 2, false, new ArgType[] { ArgType.None, ArgType.SpellUID }),
            new Command('4', "tradingWizform", 2, false, new ArgType[] { ArgType.None, ArgType.FairyUID }),
            new Command('1', "givePlayerCards", 3),
            new Command('B', "setupGambling", 3),
            new Command('6', "ifPlayerHasCards", 3, true),
            new Command('@', "ifPlayerHasSpecials", 2, true),
            new Command('=', "ifTriggerIsActive", 1, true),
            new Command('9', "removePlayerCards", 3),
            new Command(':', "moveSystem", 2),
            new Command('?', "movementSpeed", 1),
            new Command(';', "modifyWizform", 2, false, new ArgType[] { ArgType.ModifyWizform }),
            new Command('<', "lockUserInput", 1),
            new Command('>', "modifyTrigger", 3),
            new Command('A', "playAnimation", 2),
            new Command('D', "ifIsWizform", 1, true, new ArgType[] { ArgType.FairyId }),
            new Command('E', "startPrelude", 0),
            new Command('F', "npcWizFormEscapes", 0),
            new Command('G', "dance", 1),
            new Command('H', "setGlobal", 2),
            new Command('I', "beginIf_global", 2, true),
            new Command('J', "talk", 2, false, new ArgType[] { ArgType.DialogUID }),
            new Command('K', "goto", 1),
            new Command('L', "gotoRandomLabel", 2),
            new Command('M', "ask", 3),
            new Command('N', "chafferWizForms", 3),
            new Command('O', "setNpcType", 1),
            new Command('P', "deployNpcAtTrigger", 2, false, new ArgType[] { ArgType.None, ArgType.NpcUID }),
            new Command('Q', "delay", 1),
            new Command('R', "gotoLabelByRandom", 2),
            new Command('S', "ifCloseToWaypoint", 1, true),
            new Command('T', "removeWizForms", 0),
            new Command('U', "ifNpcModifierHasValue", 1, true),
            new Command('V', "setNpcModifier", 3),
            new Command('W', "defaultWizForm", 3, false, new ArgType[] { ArgType.FairyId }),
            new Command('X', "idle", 0),
            new Command('Y', "ifPlayerIsClose", 1, true),
            new Command('Z', "ifNumberOfNpcsIs", 2, true, new ArgType[] { ArgType.None, ArgType.NpcUID }),
            new Command('[', "startEffect", 2),
            new Command('\\', "setTalkLabels", 3),
            new Command(']', "setCollision", 1),
            new Command('^', "tradeWizform", 1),
            new Command('_', "createDynamicItems", 3),
            new Command('`', "playVideo", 1),
            new Command('a', "removeNpcAtTrigger", 1),
            new Command('b', "revive", 0),
            new Command('c', "lookAtTrigger", 2),
            new Command('d', "ifTriggerIsEnabled", 1, true),
            new Command('e', "playSound", 1),
            new Command('f', "playInArena", 2),
            new Command('g', "startActorEffect", 1),
            new Command('h', "endActorEffect", 0),
            new Command('i', "createSceneObjects", 1),
            new Command('j', "evolveWizForm", 1),
            new Command('k', "removeBehaviour", 1),
            new Command('l', "unlockDoor", 2),
            new Command('m', "endGame", 0),
            new Command('n', "defaultDeck", 3),
            new Command('o', "subGame", 3),
            new Command('p', "modifyEffect", 3),
            new Command('q', "playPlayerAnimation", 2),
            new Command('s', "playAmyVoice", 1),
            new Command('r', "createDynamicModel", 3),
            new Command('t', "deploySound", 2),
            new Command('u', "givePlayerPresent", 1),
            new Command('7', "endIf", 0)
        };

        public static Command byShortOp(char shortOp)
        {
            foreach (Command c in commands)
            {
                if (c.shortName == shortOp)
                    return c;
            }
            return new Command();
        }

        public static Command byLongOp(string longOp)
        {
            foreach (Command c in commands)
            {
                if (c.longName == longOp)
                    return c;
            }
            return new Command();
        }
    }

}
