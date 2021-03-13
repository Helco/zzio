using System;
using System.Threading.Tasks.Dataflow;

namespace zzmaps
{
    interface IOutput
    {
        ITargetBlock<EncodedSceneTile> CreateTileTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep);
        ITargetBlock<BuiltSceneMetadata> CreateMetaTarget(ExecutionDataflowBlockOptions options, ProgressStep progressStep);
    }
}
