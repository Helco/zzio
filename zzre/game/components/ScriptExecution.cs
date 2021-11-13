using System.Collections.Generic;
using System.Linq;
using zzio;
using zzio.script;

namespace zzre.game.components
{
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
                .Where(s => s.Trim().Length > 0 && !s.StartsWith("#"))
                .Select(s => new RawInstruction(s))
                .ToArray();

            LabelTargets = Instructions
                .Indexed()
                .Where(t => t.Value.Command == "$")
                .ToDictionary(
                    t => int.Parse(t.Value.Arguments.First()),
                    t => t.Index);
        }
    }
}
