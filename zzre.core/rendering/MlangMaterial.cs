using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Mlang.Model;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class MlangMaterial : BaseDisposable, IMaterial
{
    private readonly ShaderVariantCollection variantCollection;
    private readonly Dictionary<string, uint> options;
    private readonly Dictionary<string, BaseBinding?> bindings;
    private readonly string shaderName;
    protected readonly ShaderInfo shaderInfo;
    private IBuiltVariantPipeline? pipeline;
    private ResourceSet[]? resourceSets;

    public GraphicsDevice Device { get; }

    public IBuiltVariantPipeline Pipeline
    {
        get
        {
            if (pipeline != null)
                return pipeline;
            pipeline = variantCollection.GetBuiltPipeline(shaderInfo.VariantKeyFor(options));
            return pipeline;
        }
    }
    IBuiltPipeline IMaterial.Pipeline => Pipeline;

    public MlangMaterial(ITagContainer diContainer, string shaderName)
    {
        Device = diContainer.GetTag<GraphicsDevice>();
        variantCollection = diContainer.GetTag<ShaderVariantCollection>();
        this.shaderName = shaderName;
        shaderInfo = variantCollection.GetShaderInfo(shaderName);
        options = shaderInfo.Options.ToDictionary(o => o.Name, _ => 0u);
        bindings = new(shaderInfo.Bindings.Select(name => new KeyValuePair<string, BaseBinding?>(name, null)));
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        foreach (var binding in bindings.Values)
            binding?.Dispose();
        pipeline = null;
        ClearResourceSets();
    }

    protected void AddBinding(string name, BaseBinding binding)
    {
        if (!bindings.TryGetValue(name, out var oldBinding))
            throw new ArgumentException($"Binding {binding} does not exist for shader {shaderName}");
        oldBinding?.Dispose();
        bindings[name] = binding;
    }

    protected void SetOption(string option, bool value) => SetOption(option, value ? 1u : 0u);
    protected void SetOption(string option, uint value)
    {
        if (!options.ContainsKey(option))
            throw new ArgumentException($"Option {option} does not exist for shader {shaderName}");
        options[option] = value;
        pipeline = null;
        ClearResourceSets();
    }

    private void ClearResourceSets()
    {
        if (resourceSets != null)
        {
            foreach (var set in resourceSets)
                set.Dispose();
            resourceSets = null;
        }
    }

    public void ApplyBindings(CommandList cl)
    {
        bool isDirty = false;
        foreach (var binding in bindings.Values.NotNull())
        {
            binding.Update(cl);
            isDirty |= binding.ResetIsDirty();
        }
        if (isDirty || resourceSets == null)
        {
            ClearResourceSets();
            var setDescriptions = Pipeline.ResourceLayouts.Select((layout, i) => new ResourceSetDescription()
            {
                Layout = layout,
                BoundResources = new BindableResource[Pipeline.ShaderVariant.BindingSetSizes[i]]
            }).ToArray();
            foreach (var bindingInfo in Pipeline.ShaderVariant.Bindings)
            {
                var binding = bindings[bindingInfo.Name];
                if (binding?.Resource is null)
                    throw new InvalidOperationException($"Binding {bindingInfo.Name} is not set");
                setDescriptions[bindingInfo.SetIndex].BoundResources[bindingInfo.BindingIndex] = binding.Resource;
            }
            resourceSets = setDescriptions.Select(Device.ResourceFactory.CreateResourceSet).ToArray();
        }
        foreach (var (resourceSet, index) in resourceSets.Indexed())
            cl.SetGraphicsResourceSet((uint)index, resourceSet);
    }
}
