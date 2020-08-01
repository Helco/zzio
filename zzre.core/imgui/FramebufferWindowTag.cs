using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Veldrid;

namespace zzre.imgui
{
    public class FramebufferWindowTag : BaseDisposable
    {
        private GraphicsDevice Device { get; }
        private ResourceFactory Factory => Device.ResourceFactory;
        private ImGuiRenderer ImGuiRenderer => Window.Container.ImGuiRenderer;
        private IntPtr bindingHandle = IntPtr.Zero;
        private Texture targetColor;
        private Texture targetDepth;
        private CommandList commandList;
        private Fence fence;

        public Window Window { get; }
        public Framebuffer Framebuffer { get; private set; }
        public Pipeline? Pipeline { get; set; }
        public bool IsDirty { get; set; } = true;
        public event Action<CommandList> OnRender = _ => { };
        public event Action OnResize = () => { };

        public FramebufferWindowTag(Window parent, GraphicsDevice device)
        {
            Window = parent;
            Device = device;
            Window.AddTag(this);
            Window.OnBeforeContent += HandleBeforeContent;
            Window.OnContent += HandleContent;
            Window.OnRender += HandleRender;

            Framebuffer = null!; // Remove when Roslyn got a bit more clever
            targetColor = null!;
            targetDepth = null!;
            Resize(Window.InitialBounds.Size);
            commandList = Factory.CreateCommandList();
            fence = Factory.CreateFence(true);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            targetColor.Dispose();
            targetDepth.Dispose();
            Framebuffer.Dispose();
            commandList.Dispose();
            fence.Dispose();
            Window.OpenState = WindowOpenState.Closed;
        }

        private void Resize(Vector2 newSize)
        {
            if (newSize.X <= 0.0f || newSize.Y <= 0.0f)
                return;
            if (bindingHandle != IntPtr.Zero)
                ImGuiRenderer.RemoveImGuiBinding(targetColor);
            targetColor?.Dispose();
            targetDepth?.Dispose();
            Framebuffer?.Dispose();
            targetColor = Factory.CreateTexture(new TextureDescription(
                (uint)newSize.X, (uint)newSize.Y,
                1, 1, 1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled,
                TextureType.Texture2D));
            targetDepth = Factory.CreateTexture(new TextureDescription(
                (uint)newSize.X, (uint)newSize.Y,
                1, 1, 1,
                PixelFormat.D24_UNorm_S8_UInt,
                TextureUsage.DepthStencil,
                TextureType.Texture2D));
            Framebuffer = Factory.CreateFramebuffer(new FramebufferDescription(targetDepth, targetColor));

            bindingHandle = ImGuiRenderer.GetOrCreateImGuiBinding(Factory, targetColor);
            IsDirty = true;
            OnResize();
        }

        private void HandleBeforeContent()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        }

        private void HandleContent()
        {
            // Do not resize in render, otherwise the queued up Image has an invalid binding id
            var size = ImGui.GetContentRegionAvail();
            if (Framebuffer.Width != (uint)size.X || Framebuffer.Height != (uint)size.Y)
                Resize(size);

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.ImageButton(bindingHandle, size);
            ImGui.PopStyleVar(3);
        }

        private void HandleRender()
        {
            if (Pipeline == null)
                return;
            if (!IsDirty)
                return;
            IsDirty = false;

            fence.Reset();
            commandList.Begin();
            commandList.SetPipeline(Pipeline);
            commandList.SetFramebuffer(Framebuffer);
            commandList.ClearColorTarget(0, RgbaFloat.Clear);
            commandList.ClearDepthStencil(1f);
            OnRender(commandList);
            commandList.End();
            Device.SubmitCommands(commandList, fence);
            Window.Container.AddFenceOnce(fence);
        }
    }
}
