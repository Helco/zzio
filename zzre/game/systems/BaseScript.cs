namespace zzre.game.systems;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using DefaultEcs.System;
using Serilog;
using zzio.script;

public abstract class BaseScript<TLogContext> : AEntitySetSystem<float>
{
    private const string CmdLabel = "$";
    private const string CmdExit = "%";
    private const string CmdElse = "8";
    private const string CmdEndIf = "7";
    private const string CmdSetGlobal = "H";
    private const string CmdIfGlobal = "I";
    private const string CmdGoto = "K";
    private const string CmdGotoRandomLabel = "L";
    private const string CmdGotoLabelByRandom = "R"; // names are original ones, don't judge zzre for this ._.

    protected enum OpReturn
    {
        Continue,
        Pause,
        Stop,
        ConditionalSkip,
        UnknownInstruction
    }

    protected readonly ILogger logger;

    protected BaseScript(ITagContainer diContainer, Func<object, DefaultEcs.World, DefaultEcs.EntitySet> entitySetCreation)
        : base(diContainer.GetTag<DefaultEcs.World>(), entitySetCreation, useBuffer: true)
    {
        logger = diContainer.GetLoggerFor<TLogContext>();
    }

    protected abstract OpReturn Execute(in DefaultEcs.Entity entity, ref components.ScriptExecution script, RawInstruction instruction);

    /// <summary>
    /// Continues script execution from current point
    /// </summary>
    /// <param name="entity">The entity containing the script</param>
    /// <param name="script">The script execution state</param>
    /// <returns>Whether the script execution stopped</returns>
    protected bool Continue(in DefaultEcs.Entity entity, ref components.ScriptExecution script)
    {
        while (!script.HasStopped)
        {
            var instruction = script.Instructions[script.CurrentI];
            var opReturn = ExecuteSystem(entity, ref script, instruction);
            switch (opReturn)
            {
                case OpReturn.Continue:
                    script.CurrentI++;
                    break;

                case OpReturn.Pause:
                    script.CurrentI++;
                    return true;

                case OpReturn.Stop:
                    script.CurrentI = script.Instructions.Count;
                    return false;

                case OpReturn.ConditionalSkip:
                    BaseScript<TLogContext>.ConditionalSkip(ref script);
                    break;

                case OpReturn.UnknownInstruction:
                    logger.Error("Unknown script instruction at {Index}: {@Instruction}", script.CurrentI, instruction);
                    script.CurrentI++;
                    return true;

                default: throw new NotImplementedException($"Unimplemented op return type {opReturn}");
            }
        }
        return false;
    }

    private OpReturn ExecuteSystem(in DefaultEcs.Entity entity, ref components.ScriptExecution script, RawInstruction instruction)
    {
        switch (instruction.Command)
        {
            case CmdLabel:
            case CmdEndIf:
                return OpReturn.Continue;

            case CmdExit:
                return OpReturn.Stop;

            case CmdElse:
                return OpReturn.ConditionalSkip;

            case CmdGoto:
                var label = int.Parse(instruction.Arguments.Single());
                script.CurrentI = script.LabelTargets[label];
                return OpReturn.Continue;

            case CmdGotoRandomLabel:
                var range = int.Parse(instruction.Arguments[0]);
                var start = int.Parse(instruction.Arguments[1]);
                label = start + Random.Shared.Next(range);
                script.CurrentI = script.LabelTargets[label];
                return OpReturn.Continue;

            case CmdGotoLabelByRandom:
                // a percentage of *not* jumping to a label -_-
                var percentage = int.Parse(instruction.Arguments[0]);
                label = int.Parse(instruction.Arguments[1]);
                if (Random.Shared.Next(100) >= percentage)
                    script.CurrentI = script.LabelTargets[label];
                return OpReturn.Continue;

            case CmdSetGlobal:
            case CmdIfGlobal:
                // TODO: Add global variables
                logger.Error("Global variables are not implemented yet");
                return OpReturn.Continue;

            default:
                return Execute(entity, ref script, instruction);
        }
    }

    private static void ConditionalSkip(ref components.ScriptExecution script)
    {
        script.CurrentI++; // jump over skipping instruction
        for (; script.CurrentI < script.Instructions.Count; script.CurrentI++)
        {
            var command = script.Instructions[script.CurrentI].Command;
            if (command == CmdElse || command == CmdEndIf)
                break;
        }
        script.CurrentI++; // prevent else from executing (triggering another skip)
    }

    protected void LogUnimplementedInstructionWarning([CallerMemberName] string method = "<not set>") =>
        logger.Warning("Unimplemented instruction \"{Name}\"", method);
}
