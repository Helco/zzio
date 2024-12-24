using System.Linq;
using zzio.script;

namespace zzre.game.systems.ui;

partial class UIScript
{
    private const string CmdModifyWizform = ";";
    private const string CmdIfIsWizform = "D";

    protected override OpReturn Execute(in DefaultEcs.Entity entity, ref components.ScriptExecution script, RawInstruction instruction)
    {
        var args = instruction.Arguments;
        ref var uiScript = ref entity.Get<components.ui.UIScript>();
        switch (instruction.Command)
        {
            case CmdModifyWizform:
                uiScript.ItemConsumed = ModifyWizform(entity, (ModifyWizformType)int.Parse(args[0]), int.Parse(args[1]));
                return OpReturn.Continue;
            case CmdIfIsWizform:
                return IfIsWizform(entity, int.Parse(args.Single())) ? OpReturn.Continue : OpReturn.ConditionalSkip;
            default:
                return OpReturn.UnknownInstruction;
        }
    }
}
