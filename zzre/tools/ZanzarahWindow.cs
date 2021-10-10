using System;
using Veldrid;
using zzre.game;
using zzre.imgui;

namespace zzre.tools
{
    public class ZanzarahWindow : IZanzarahContainer
    {
        private readonly ITagContainer diContainer;
        private readonly FramebufferArea fbArea;

        public event Action OnResize
        {
            add => fbArea.OnResize += value;
            remove => fbArea.OnResize -= value;
        }

        public event Action<Key> OnKeyDown
        {
            add => Window.OnKeyDown += value;
            remove => Window.OnKeyDown -= value;
        }

        public event Action<Key> OnKeyUp
        {
            add => Window.OnKeyUp += value;
            remove => Window.OnKeyUp -= value;
        }

        public Window Window { get; }
        public Zanzarah Zanzarah { get; }
        public Framebuffer Framebuffer => fbArea.Framebuffer;

        public ZanzarahWindow(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Zanzarah");
            Window.AddTag(this);
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1040, 800); // a bit more to compensate for borders (about)

            fbArea = new FramebufferArea(Window, diContainer.GetTag<GraphicsDevice>());
            Zanzarah = new Zanzarah(diContainer, this);
            Window.AddTag(Zanzarah);

            Window.OnContent += HandleContent;
            fbArea.OnRender += Zanzarah.Render;
        }

        private void HandleContent()
        {
            Zanzarah.Update();
            fbArea.IsDirty = true;
            fbArea.Content();
        }
    }
}
