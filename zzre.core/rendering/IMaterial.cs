using System;
using Veldrid;

namespace zzre.rendering;

public interface IMaterial : IDisposable
{
    GraphicsDevice Device { get; }
    IBuiltPipeline Pipeline { get; }

    void Apply(CommandList cl)
    {
        ApplyPipeline(cl);
        ApplyBindings(cl);
    }
    void ApplyPipeline(CommandList cl) => cl.SetPipeline(Pipeline.Pipeline);
    void ApplyBindings(CommandList cl);
}

public interface ITexturedMaterial : IMaterial
{
    TextureBinding Texture { get; }
}
