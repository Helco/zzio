using System;
using System.Linq;
using DefaultEcs.System;
using zzio.script;

namespace zzre.game.systems
{
    public abstract class BaseScript : AEntitySetSystem<float>
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

        protected BaseScript(ITagContainer diContainer, Func<object, DefaultEcs.World, DefaultEcs.EntitySet> entitySetCreation)
            : base(diContainer.GetTag<DefaultEcs.World>(), entitySetCreation, null, 0)
        {
        }

        protected abstract OpReturn Execute(in DefaultEcs.Entity entity, ref components.ScriptExecution script, RawInstruction instruction);

        /// <summary>
        /// Continues script execution from current point
        /// </summary>
        /// <param name="entity">The entity containing the script</param>
        /// <param name="script">The script execution state</param>
        /// <returns>Whether the script can continue further</returns>
        protected bool Continue(in DefaultEcs.Entity entity, ref components.ScriptExecution script)
        {
            while (!script.HasStopped)
            {
                var opReturn = ExecuteSystem(entity, ref script, script.Instructions[script.CurrentI]);
                switch (opReturn)
                {
                    case OpReturn.Continue:
                        script.CurrentI++;
                        break;

                    case OpReturn.Pause:
                        script.CurrentI++;
                        return !script.HasStopped;

                    case OpReturn.Stop:
                        script.CurrentI = script.Instructions.Count;
                        return false;

                    case OpReturn.ConditionalSkip:
                        ConditionalSkip(ref script);
                        break;

                    case OpReturn.UnknownInstruction: throw new InvalidInstructionException();
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
                    label = start + GlobalRandom.Get.Next(range);
                    script.CurrentI = script.LabelTargets[label];
                    return OpReturn.Continue;

                case CmdGotoLabelByRandom:
                    // a percentage of *not* jumping to a label -_-
                    var percentage = int.Parse(instruction.Arguments[0]);
                    label = int.Parse(instruction.Arguments[1]);
                    if (GlobalRandom.Get.Next(100) >= percentage)
                        script.CurrentI = script.LabelTargets[label];
                    return OpReturn.Continue;

                case CmdSetGlobal:
                case CmdIfGlobal:
                    // TODO: Add global variables
                    throw new NotImplementedException("Global variables are not implemented yet");

                default:
                    return Execute(entity, ref script, instruction);
            }
        }

        private void ConditionalSkip(ref components.ScriptExecution script)
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
    }
}
