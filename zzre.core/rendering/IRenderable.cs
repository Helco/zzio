using Veldrid;

namespace zzre.rendering
{
    public interface IRenderable
    {
        void Render(CommandList cl);
    }
}
