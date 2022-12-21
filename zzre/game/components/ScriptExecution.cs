using System.Collections.Generic;
using System.Linq;
using zzio;
using zzio.script;

namespace zzre.game.components;

public class ScriptExecution // reference type as we have to have reference members as well
{
    public IReadOnlyList<RawInstruction> Instructions { get; }
    public IReadOnlyDictionary<int, int> LabelTargets { get; }
    public int CurrentI;

    public bool HasStopped => CurrentI >= Instructions.Count;

    public ScriptExecution(string script)
    {
        Instructions = script
            .Split("\n")
            .Where(s => s.Trim().Length > 0 && !s.StartsWith("//"))
            .Select(s => new RawInstruction(s))
            .Select(Compile)
            .ToArray();

        LabelTargets = Instructions
            .Indexed()
            .Where(t => t.Value.Command == "$")
            .ToDictionary(
                t => int.Parse(t.Value.Arguments.First()),
                t => t.Index);
    }

    // TODO: Replace ScriptExecution ctor with actual compiler

    private static RawInstruction Compile(RawInstruction raw) => LongToShortCommand.TryGetValue(raw.Command, out var shortCommand) ?
        new RawInstruction(shortCommand, raw.Arguments)
        : raw;

    private static readonly IReadOnlyDictionary<string, string> LongToShortCommand = new Dictionary<string, string>()
    {
        { "say", "!" },
        { "setModel", "C" },
        { "choice", "\"" },
        { "waitForUser", "#" },
        { "label", "$" },
        { "setCamera", "&" },
        { "exit", "%" },
        { "wizform", "'" },
        { "spell", "(" },
        { "else", "8" },
        { "changeWaypoint", ")" },
        { "fight", "*" },
        { "lookAtPlayer", "+" },
        { "changeDatabase", "," },
        { "removeNpc", "-" },
        { "catchWizform", "." },
        { "killPlayer", "0" },
        { "tradingCurrency", "5" },
        { "tradingItem", "2" },
        { "tradingSpell", "3" },
        { "tradingWizform", "4" },
        { "givePlayerCards", "1" },
        { "setupGambling", "B" },
        { "ifPlayerHasCards", "6" },
        { "ifPlayerHasSpecials", "@" },
        { "ifTriggerIsActive", "=" },
        { "removePlayerCards", "9" },
        { "moveSystem", ":" },
        { "movementSpeed", "?" },
        { "modifyWizform", ";" },
        { "lockUserInput", "<" },
        { "modifyTrigger", ">" },
        { "playAnimation", "A" },
        { "ifIsWizform", "D" },
        { "startPrelude", "E" },
        { "npcWizFormEscapes", "F" },
        { "dance", "G" },
        { "setGlobal", "H" },
        { "beginIf_global", "I" },
        { "talk", "J" },
        { "goto", "K" },
        { "gotoRandomLabel", "L" },
        { "ask", "M" },
        { "chafferWizForms", "N" },
        { "setNpcType", "O" },
        { "deployNpcAtTrigger", "P" },
        { "delay", "Q" },
        { "gotoLabelByRandom", "R" },
        { "ifCloseToWaypoint", "S" },
        { "removeWizForms", "T" },
        { "ifNpcModifierHasValue", "U" },
        { "setNpcModifier", "V" },
        { "defaultWizForm", "W" },
        { "idle", "X" },
        { "ifPlayerIsClose", "Y" },
        { "ifNumberOfNpcsIs", "Z" },
        { "startEffect", "[" },
        { "setTalkLabels", "\\" },
        { "setCollision", "]" },
        { "tradeWizform", "^" },
        { "createDynamicItems", "_" },
        { "playVideo", "`" },
        { "removeNpcAtTrigger", "a" },
        { "revive", "b" },
        { "lookAtTrigger", "c" },
        { "ifTriggerIsEnabled", "d" },
        { "playSound", "e" },
        { "playInArena", "f" },
        { "startActorEffect", "g" },
        { "endActorEffect", "h" },
        { "createSceneObjects", "i" },
        { "evolveWizForm", "j" },
        { "removeBehavior", "k" },
        { "unlockDoor", "l" },
        { "endGame", "m" },
        { "defaultDeck", "n" },
        { "subGame", "o" },
        { "modifyEffect", "p" },
        { "playPlayerAnimation", "q" },
        { "playAmyVoice", "s" },
        { "createDynamicModel", "r" },
        { "deploySound", "t" },
        { "givePlayerPresent", "u" },
        { "endIf", "7" }
    };
}
