using Veldrid;

namespace zzre.rendering;

public class SamplerBinding : BaseBinding
{
    private bool ownsSampler;
    private bool isContentDirty;
    private Sampler? sampler;
    private SamplerDescription description = SamplerDescription.Point;

    public Sampler Sampler
    {
        get
        {
            if (sampler != null)
                return sampler;
            sampler = Parent.Device.ResourceFactory.CreateSampler(description);
            ownsSampler = true;
            isBindingDirty = true;
            return sampler;
        }
        set
        {
            if (ownsSampler)
                sampler?.Dispose();
            sampler = value;
            ownsSampler = false;
            isBindingDirty = true;
        }
    }

    public ref SamplerDescription Ref
    {
        get
        {
            isContentDirty = true;
            isBindingDirty = true; // the sampler *has* to be regenerated
            return ref description;
        }
    }
    public SamplerDescription Value
    {
        get => description;
        set => Ref = value;
    }

    public override BindableResource? Resource => Sampler;

    public SamplerBinding(IMaterial parent) : base(parent) { }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        if (ownsSampler)
            sampler?.Dispose();
    }

    public override void Update(CommandList cl)
    {
        if (!isContentDirty || !ownsSampler)
            return;
        sampler?.Dispose();
        sampler = Parent.Device.ResourceFactory.CreateSampler(description);
        isContentDirty = false;
    }
}
