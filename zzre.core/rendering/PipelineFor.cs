using System;
using System.Collections.Generic;

namespace zzre.rendering
{
    public class PipelineFor<T>
    {
        public IBuiltPipeline Pipeline { get; }

        private PipelineFor(IBuiltPipeline pipeline)
        {
            Pipeline = pipeline;
        }

        public static IBuiltPipeline Get(ITagContainer diContainer, Func<IPipelineBuilder, IBuiltPipeline> create)
        {
            if (diContainer.HasTag<PipelineFor<T>>())
                return diContainer.GetTag<PipelineFor<T>>().Pipeline;
            var holder = new PipelineFor<T>(
                create(diContainer.GetTag<PipelineCollection>().GetPipeline()));
            diContainer.AddTag(holder);
            return holder.Pipeline;
        }
    }
}
