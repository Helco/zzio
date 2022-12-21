using Veldrid;

namespace zzre.rendering;

public class TextureBinding : BaseBinding
{
    private BindableResource? resource = null;
    public override BindableResource? Resource => resource;

    public Texture? Texture
    {
        get => resource as Texture;
        set
        {
            isBindingDirty = resource != value;
            resource = value;
        }
    }

    public TextureView? TextureView
    {
        get => resource as TextureView;
        set
        {
            isBindingDirty = resource != value;
            resource = value;
        }
    }

    public TextureBinding(IMaterial parent) : base(parent) { }

    public override void Update(CommandList cl) { }
}
