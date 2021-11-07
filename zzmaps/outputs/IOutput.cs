using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    internal interface IOutput
    {
        ITargetBlock<EncodedSceneTile> CreateTileTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep);
        ITargetBlock<BuiltSceneMetadata> CreateMetaTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep);
    }
}
