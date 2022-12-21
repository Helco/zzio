using System;
using System.Collections.Generic;
using System.Linq;
using Veldrid;

namespace zzre.rendering;

public interface IMaterial : IDisposable
{
    GraphicsDevice Device { get; }
    IBuiltPipeline Pipeline { get; }

    // please implement some of these
    IEnumerable<TextureBinding> TextureBindings => Bindings.OfType<TextureBinding>();
    IEnumerable<UniformBinding> UniformBindings => Bindings.OfType<UniformBinding>();
    IEnumerable<SamplerBinding> SamplerBindings => Bindings.OfType<SamplerBinding>();
    IEnumerable<BaseBinding> Bindings => TextureBindings.Cast<BaseBinding>().Concat(SamplerBindings).Concat(UniformBindings);

    void Apply(CommandList cl)
    {
        ApplyPipeline(cl);
        ApplyBindings(cl);
    }
    void ApplyPipeline(CommandList cl) => cl.SetPipeline(Pipeline.Pipeline);
    void ApplyBindings(CommandList cl);
}
