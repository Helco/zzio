using System;
using System.Collections.Generic;
using System.Text;
using Veldrid;
using zzre.rendering;

namespace zzre.materials
{

    public class ModelStandardMaterial : BaseMaterial
    {
        public TextureBinding MainTexture { get; }
        public SamplerBinding Sampler { get; }
        public UniformBinding<ModelStandardTransformationUniforms> Transformation { get; }
        public UniformBinding<ModelStandardMaterialUniforms> Uniforms { get; }

        public ModelStandardMaterial(ITagContainer diContainer) : base(diContainer.GetTag<GraphicsDevice>(),
            diContainer.GetTag<StandardPipelines>().ModelStandard)
        {
            Configure()
                .Add(MainTexture = new TextureBinding(this))
                .Add(Sampler = new SamplerBinding(this))
                .Add(Transformation = new UniformBinding<ModelStandardTransformationUniforms>(this))
                .Add(Uniforms = new UniformBinding<ModelStandardMaterialUniforms>(this))
                .NextBindingSet();
        }
    }
}
