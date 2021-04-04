using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using static ImGuiNET.ImGui;

namespace zzre.imgui
{
    public class FramebufferArea : BaseDisposable
    {
        private readonly GraphicsDevice Device;
        private ResourceFactory Factory => Device.ResourceFactory;
        private readonly WindowContainer WindowContainer;
        private ImGuiRenderer ImGuiRenderer => WindowContainer.ImGuiRenderer;

        private IntPtr bindingHandle = IntPtr.Zero;
        private Texture targetColor;
        private Texture targetDepth;
        private CommandList commandList;
        private Fence fence;
        private OnceAction onceBeforeContent = new OnceAction();

        public Framebuffer Framebuffer { get; private set; }
        public RgbaFloat ClearColor { get; set; }
        public float Ratio => Framebuffer.Width / (float)Framebuffer.Height;
        public bool IsDirty { get; set; } = true;
        public event Action<CommandList> OnRender = _ => { };
        public event Action OnResize = () => { };

        public FramebufferArea(Window parent, GraphicsDevice device)
        {
            WindowContainer = parent.Container;
            Device = device;
            parent.AddTag(this);
            parent.OnRender += HandleRender;

            Framebuffer = null!; // Remove when Roslyn got a bit more clever
            targetColor = null!;
            targetDepth = null!;
            Resize(parent.InitialBounds.Size);
            commandList = Factory.CreateCommandList();
            fence = Factory.CreateFence(true);

            ClearColor = new RgbaFloat(GetStyle().Colors[(int)ImGuiCol.FrameBg]);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            targetColor.Dispose();
            targetDepth.Dispose();
            Framebuffer.Dispose();
            commandList.Dispose();
            fence.Dispose();
        }

        private void Resize(Vector2 newSize)
        {
            if (newSize.X <= 0.0f || newSize.Y <= 0.0f)
                return;
            if (bindingHandle != IntPtr.Zero)
                ImGuiRenderer.RemoveImGuiBinding(targetColor);
            var oldTargetColor = targetColor;
            var oldTargetDepth = targetDepth;
            var oldFramebuffer = Framebuffer;
            onceBeforeContent.Next += () =>
            {
                // delaying the disposal works around a freeze bug related to Vulkan
                oldTargetColor?.Dispose();
                oldTargetDepth?.Dispose();
                oldFramebuffer?.Dispose();
            };

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

        public void Content()
        {
            onceBeforeContent.Invoke();

            // Do not resize in render, otherwise the queued up Image has an invalid binding id
            var offset = GetCursorScreenPos();
            var size = GetContentRegionAvail();
            if (Framebuffer.Width != (uint)size.X || Framebuffer.Height != (uint)size.Y)
                Resize(size);

            Vector4 normalColor;
            unsafe { normalColor = *GetStyleColorVec4(ImGuiCol.Button); }
            PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
            PushStyleColor(ImGuiCol.ButtonActive, normalColor);
            PushStyleColor(ImGuiCol.ButtonHovered, normalColor);
            Image(bindingHandle, size);
            PopStyleColor(2);
            PopStyleVar(2);

            ImGuizmoNET.ImGuizmo.SetRect(offset.X, offset.Y, size.X, size.Y);
        }

        private void HandleRender()
        {
            if (!IsDirty)
                return;
            IsDirty = false;

            fence.Reset();
            commandList.Begin();
            commandList.SetFramebuffer(Framebuffer);
            commandList.ClearColorTarget(0, ClearColor);
            commandList.ClearDepthStencil(1f);
            OnRender(commandList);
            commandList.End();
            Device.SubmitCommands(commandList, fence);
            WindowContainer.AddFenceOnce(fence);
        }
    }
}
