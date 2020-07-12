using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using zzio.script;
using CommandHandler = System.Action<System.IO.TextWriter, zzio.script.RawInstruction[], string[]>;


namespace zzsc
{
    public class CLI
    {
        private static readonly IReadOnlyDictionary<char, string> CommandByShort = new Dictionary<char, string>()
        {
            {'!', "say" },
            {'C', "setModel" },
            {'"', "choice" },
            {'#', "waitForUser" },
            {'$', "label" },
            {'&', "setCamera" },
            {'%', "exit" },
            {'\'', "wizform" },
            {'(', "spell" },
            {'8', "else" },
            {')', "changeWaypoint" },
            {'*', "fight" },
            {'+', "lookAtPlayer" },
            {',', "changeDatabase" },
            {'-', "removeNpc" },
            {'.', "catchWizform" },
            {'0', "killPlayer" },
            {'5', "tradingCurrency" },
            {'2', "tradingItem" },
            {'3', "tradingSpell" },
            {'4', "tradingWizform" },
            {'1', "givePlayerCards" },
            {'B', "setupGambling" },
            {'6', "ifPlayerHasCards" },
            {'@', "ifPlayerHasSpecials" },
            {'=', "ifTriggerIsActive" },
            {'9', "removePlayerCards" },
            {':', "moveSystem" },
            {'?', "movementSpeed" },
            {';', "modifyWizform" },
            {'<', "lockUserInput" },
            {'>', "modifyTrigger" },
            {'A', "playAnimation" },
            {'D', "ifIsWizform" },
            {'E', "startPrelude" },
            {'F', "npcWizFormEscapes" },
            {'G', "dance" },
            {'H', "setGlobal" },
            {'I', "beginIf_global" },
            {'J', "talk" },
            {'K', "goto" },
            {'L', "gotoRandomLabel" },
            {'M', "ask" },
            {'N', "chafferWizForms" },
            {'O', "setNpcType" },
            {'P', "deployNpcAtTrigger" },
            {'Q', "delay" },
            {'R', "gotoLabelByRandom" },
            {'S', "ifCloseToWaypoint" },
            {'T', "removeWizForms" },
            {'U', "ifNpcModifierHasValue" },
            {'V', "setNpcModifier" },
            {'W', "defaultWizForm" },
            {'X', "idle" },
            {'Y', "ifPlayerIsClose" },
            {'Z', "ifNumberOfNpcsIs" },
            {'[', "startEffect" },
            {'\\', "setTalkLabels" },
            {']', "setCollision" },
            {'^', "tradeWizform" },
            {'_', "createDynamicItems" },
            {'`', "playVideo" },
            {'a', "removeNpcAtTrigger" },
            {'b', "revive" },
            {'c', "lookAtTrigger" },
            {'d', "ifTriggerIsEnabled" },
            {'e', "playSound" },
            {'f', "playInArena" },
            {'g', "startActorEffect" },
            {'h', "endActorEffect" },
            {'i', "createSceneObjects" },
            {'j', "evolveWizForm" },
            {'k', "removeBehaviour" },
            {'l', "unlockDoor" },
            {'m', "endGame" },
            {'n', "defaultDeck" },
            {'o', "subGame" },
            {'p', "modifyEffect" },
            {'q', "playPlayerAnimation" },
            {'s', "playAmyVoice" },
            {'r', "createDynamicModel" },
            {'t', "deploySound" },
            {'u', "givePlayerPresent" },
            {'7', "endIf" }
        };

        private static IReadOnlyCollection<string> SectionStartCommands = new HashSet<string>()
        {
            "label",
            "else",
            "ifPlayerHasCards",
            "ifPlayerHasSpecials",
            "ifTriggerIsActive",
            "ifIsWizform",
            "beginIf_global",
            "ifCloseToWaypoint",
            "ifNpcModifierHasValue",
            "ifPlayerIsClose",
            "ifNumberOfNpcsIs",
            "ifTriggerIsEnabled"
        };

        private static IReadOnlyCollection<string> SectionEndCommands = new HashSet<string>()
        {
            "label",
            "else",
            "endIf"
        };

        private static IReadOnlyDictionary<string, char> _commandByLong = null;
        private static IReadOnlyDictionary<string, char> CommandByLong
        {
            get
            {
                if (_commandByLong == null)
                    _commandByLong = CommandByShort.ToDictionary(p => p.Value, p => p.Key);
                return _commandByLong;
            }
        }

        public static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("nope");
                return;
            }

            string command = args[0].ToLower();
            string inputPath = args[1];
            string outputPath = args[2];

            IReadOnlyDictionary<string, CommandHandler> commands = new Dictionary<string, CommandHandler>()
            {
                { "compile", compile },
                { "decompile", decompile }
            };
            if (!commands.ContainsKey(command))
            {
                Console.WriteLine("What is " + command + "?");
                return;
            }

            TextReader input = inputPath == "stdin"
                ? Console.In
                : new StreamReader(inputPath);
            TextWriter output = outputPath == "stdout"
                ? Console.Out
                : new StreamWriter(outputPath);

            List<RawInstruction> instructions = new List<RawInstruction>();
            string line;
            while ((line = input.ReadLine()) != null && line != "EOF")
            {
                int i;
                if ((i = line.IndexOf("#")) >= 0)
                    line = line.Substring(0, i).Trim();
                if (line.Length > 0)
                    instructions.Add(new RawInstruction(line));
            }

            commands[command](output, instructions.ToArray(), args.Skip(3).ToArray());
            output.Close();
        }

        static void decompile(TextWriter output, RawInstruction[] instructions, string[] _)
        {
            int prefixLength = 4;
            int prefixTabs = 0;
            foreach (var _i in instructions)
            {
                var i = _i;
                if (i.Command.Length == 1)
                    i = new RawInstruction(CommandByShort[i.Command[0]], i.Arguments);
                if (SectionEndCommands.Contains(i.Command))
                    prefixTabs = Math.Max(0, prefixTabs - 1);
                output.Write(string.Join("", Enumerable.Repeat(" ", prefixTabs * prefixLength)));
                output.WriteLine(i);
                if (SectionStartCommands.Contains(i.Command))
                    prefixTabs++;
            }
        }

        static void compile(TextWriter output, RawInstruction[] instructions, string[] _)
        {
            foreach (var _i in instructions)
            {
                var i = _i;
                if (i.Command.Length == 1)
                    i = new RawInstruction(CommandByShort[i.Command[0]], i.Arguments);
                output.WriteLine(i);
            }
        }

    }
}
