// This is a forked version of Veldrid.ImGui, which is licensed under MIT:
/*
The MIT License (MIT)

Copyright (c) 2017 Eric Mellino and Veldrid contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/
#nullable disable

using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.IO;
using Veldrid;
using Silk.NET.SDL;

using Texture = Veldrid.Texture;
using PixelFormat = Veldrid.PixelFormat;

namespace zzre.imgui
{
    /// <summary>
    /// Can render draw lists produced by ImGui.
    /// Also provides functions for updating ImGui input.
    /// </summary>
    public class ImGuiRenderer : IDisposable
    {
        private GraphicsDevice _gd;
        private readonly Assembly _assembly;
        private ColorSpaceHandling _colorSpaceHandling;

        // Device objects
        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _indexBuffer;
        private DeviceBuffer _projMatrixBuffer;
        private Texture _fontTexture;
        private Shader _vertexShader;
        private Shader _fragmentShader;
        private ResourceLayout _layout;
        private ResourceLayout _textureLayout;
        private Pipeline _pipeline;
        private ResourceSet _mainResourceSet;
        private ResourceSet _fontTextureResourceSet;
        private IntPtr _fontAtlasID = (IntPtr)1;

        private int _windowWidth;
        private int _windowHeight;
        private Vector2 _scaleFactor = Vector2.One;

        // Image trackers
        private readonly Dictionary<TextureView, ResourceSetInfo> _setsByView
            = new Dictionary<TextureView, ResourceSetInfo>();
        private readonly Dictionary<Texture, TextureView> _autoViewsByTexture
            = new Dictionary<Texture, TextureView>();
        private readonly Dictionary<IntPtr, ResourceSetInfo> _viewsById = new Dictionary<IntPtr, ResourceSetInfo>();
        private readonly List<IDisposable> _ownedResources = new List<IDisposable>();
        private int _lastAssignedID = 100;
        private bool _frameBegun;

        /// <summary>
        /// Constructs a new ImGuiRenderer.
        /// </summary>
        /// <param name="gd">The GraphicsDevice used to create and update resources.</param>
        /// <param name="outputDescription">The output format.</param>
        /// <param name="width">The initial width of the rendering target. Can be resized.</param>
        /// <param name="height">The initial height of the rendering target. Can be resized.</param>
        /// <param name="colorSpaceHandling">Identifies how the renderer should treat vertex colors.</param>
        /// <param name="callNewFrame">Whether a new frame should be started. If false you have to call ManualNewFrame() yourself.</param>
        public ImGuiRenderer(GraphicsDevice gd, OutputDescription outputDescription, int width, int height, ColorSpaceHandling colorSpaceHandling, bool callNewFrame)
        {
            _gd = gd;
            _assembly = typeof(ImGuiRenderer).GetTypeInfo().Assembly;
            _colorSpaceHandling = colorSpaceHandling;
            _windowWidth = width;
            _windowHeight = height;

            IntPtr context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            ImGui.GetIO().Fonts.AddFontDefault();
            ImGui.GetIO().Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;

            CreateDeviceResources(gd, outputDescription);

            SetPerFrameImGuiData(1f / 60f);

            if (callNewFrame)
            {
                ManualNewFrame();
            }
        }

        public void ManualNewFrame()
        {
            if (_frameBegun)
            {
                ImGui.EndFrame();
            }

            ImGui.NewFrame();
            _frameBegun = true;
        }

        public void DestroyDeviceObjects()
        {
            Dispose();
        }

        public void CreateDeviceResources(GraphicsDevice gd, OutputDescription outputDescription)
            => CreateDeviceResources(gd, outputDescription, _colorSpaceHandling);
        public void CreateDeviceResources(GraphicsDevice gd, OutputDescription outputDescription, ColorSpaceHandling colorSpaceHandling)
        {
            _gd = gd;
            _colorSpaceHandling = colorSpaceHandling;
            ResourceFactory factory = gd.ResourceFactory;
            _vertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            _vertexBuffer.Name = "ImGui.NET Vertex Buffer";
            _indexBuffer = factory.CreateBuffer(new BufferDescription(2000, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
            _indexBuffer.Name = "ImGui.NET Index Buffer";

            _projMatrixBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            _projMatrixBuffer.Name = "ImGui.NET Projection Buffer";

            byte[] vertexShaderBytes = GetEmbeddedResourceBytes("imgui-vertex");
            byte[] fragmentShaderBytes = GetEmbeddedResourceBytes("imgui-frag");
            _vertexShader = factory.CreateShader(new ShaderDescription(ShaderStages.Vertex, vertexShaderBytes, _gd.BackendType == GraphicsBackend.Vulkan ? "main" : "VS"));
            _vertexShader.Name = "ImGui.NET Vertex Shader";
            _fragmentShader = factory.CreateShader(new ShaderDescription(ShaderStages.Fragment, fragmentShaderBytes, _gd.BackendType == GraphicsBackend.Vulkan ? "main" : "FS"));
            _fragmentShader.Name = "ImGui.NET Fragment Shader";

            VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[]
            {
                new VertexLayoutDescription(
                    new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2),
                    new VertexElementDescription("in_texCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                    new VertexElementDescription("in_color", VertexElementSemantic.Color, VertexElementFormat.Byte4_Norm))
            };

            _layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)));
            _layout.Name = "ImGui.NET Resource Layout";
            _textureLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("MainTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment)));
            _textureLayout.Name = "ImGui.NET Texture Layout";

            GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                new DepthStencilStateDescription(false, false, ComparisonKind.Always),
                new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, true, true),
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    vertexLayouts,
                    new[] { _vertexShader, _fragmentShader },
                    new[]
                    {
                        new SpecializationConstant(0, gd.IsClipSpaceYInverted),
                        new SpecializationConstant(1, _colorSpaceHandling == ColorSpaceHandling.Legacy),
                    }),
                new ResourceLayout[] { _layout, _textureLayout },
                outputDescription,
                ResourceBindingModel.Default);
            _pipeline = factory.CreateGraphicsPipeline(ref pd);
            _pipeline.Name = "ImGui.NET Pipeline";

            _mainResourceSet = factory.CreateResourceSet(new ResourceSetDescription(_layout,
                _projMatrixBuffer,
                gd.PointSampler));
            _mainResourceSet.Name = "ImGui.NET Main Resource Set";

            RecreateFontDeviceTexture(gd);
        }

        /// <summary>
        /// Gets or creates a handle for a texture to be drawn with ImGui.
        /// Pass the returned handle to Image() or ImageButton().
        /// </summary>
        public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, TextureView textureView)
        {
            if (!_setsByView.TryGetValue(textureView, out ResourceSetInfo rsi))
            {
                ResourceSet resourceSet = factory.CreateResourceSet(new ResourceSetDescription(_textureLayout, textureView));
                resourceSet.Name = $"ImGui.NET {textureView.Name} Resource Set";
                rsi = new ResourceSetInfo(GetNextImGuiBindingID(), resourceSet);

                _setsByView.Add(textureView, rsi);
                _viewsById.Add(rsi.ImGuiBinding, rsi);
                _ownedResources.Add(resourceSet);
            }

            return rsi.ImGuiBinding;
        }

        public void RemoveImGuiBinding(TextureView textureView)
        {
            if (_setsByView.TryGetValue(textureView, out ResourceSetInfo rsi))
            {
                _setsByView.Remove(textureView);
                _viewsById.Remove(rsi.ImGuiBinding);
                _ownedResources.Remove(rsi.ResourceSet);
                rsi.ResourceSet.Dispose();
            }
        }

        private IntPtr GetNextImGuiBindingID()
        {
            int newID = _lastAssignedID++;
            return (IntPtr)newID;
        }

        /// <summary>
        /// Gets or creates a handle for a texture to be drawn with ImGui.
        /// Pass the returned handle to Image() or ImageButton().
        /// </summary>
        public IntPtr GetOrCreateImGuiBinding(ResourceFactory factory, Texture texture)
        {
            if (!_autoViewsByTexture.TryGetValue(texture, out TextureView textureView))
            {
                textureView = factory.CreateTextureView(texture);
                textureView.Name = $"ImGui.NET {texture.Name} View";
                _autoViewsByTexture.Add(texture, textureView);
                _ownedResources.Add(textureView);
            }

            return GetOrCreateImGuiBinding(factory, textureView);
        }

        public void RemoveImGuiBinding(Texture texture)
        {
            if (_autoViewsByTexture.TryGetValue(texture, out TextureView textureView))
            {
                _autoViewsByTexture.Remove(texture);
                _ownedResources.Remove(textureView);
                textureView.Dispose();
                RemoveImGuiBinding(textureView);
            }
        }

        /// <summary>
        /// Retrieves the shader texture binding for the given helper handle.
        /// </summary>
        public ResourceSet GetImageResourceSet(IntPtr imGuiBinding)
        {
            if (!_viewsById.TryGetValue(imGuiBinding, out ResourceSetInfo rsi))
            {
                throw new InvalidOperationException("No registered ImGui binding with id " + imGuiBinding.ToString());
            }

            return rsi.ResourceSet;
        }

        public void ClearCachedImageResources()
        {
            foreach (IDisposable resource in _ownedResources)
            {
                resource.Dispose();
            }

            _ownedResources.Clear();
            _setsByView.Clear();
            _viewsById.Clear();
            _autoViewsByTexture.Clear();
            _lastAssignedID = 100;
        }

        private byte[] GetEmbeddedResourceBytes(string resourceName)
        {
            using (Stream s = _assembly.GetManifestResourceStream(resourceName))
            {
                byte[] ret = new byte[s.Length];
                s.Read(ret, 0, (int)s.Length);
                return ret;
            }
        }

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public unsafe void RecreateFontDeviceTexture() => RecreateFontDeviceTexture(_gd);

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public unsafe void RecreateFontDeviceTexture(GraphicsDevice gd)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            // Build
            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);

            // Store our identifier
            io.Fonts.SetTexID(_fontAtlasID);

            _fontTexture?.Dispose();
            _fontTexture = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                (uint)width,
                (uint)height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled));
            _fontTexture.Name = "ImGui.NET Font Texture";
            gd.UpdateTexture(
                _fontTexture,
                (IntPtr)pixels,
                (uint)(bytesPerPixel * width * height),
                0,
                0,
                0,
                (uint)width,
                (uint)height,
                1,
                0,
                0);

            _fontTextureResourceSet?.Dispose();
            _fontTextureResourceSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_textureLayout, _fontTexture));
            _fontTextureResourceSet.Name = "ImGui.NET Font Texture Resource Set";

            io.Fonts.ClearTexData();
        }

        /// <summary>
        /// Renders the ImGui draw list data.
        /// </summary>
        public unsafe void Render(GraphicsDevice gd, CommandList cl)
        {
            if (_frameBegun)
            {
                _frameBegun = false;
                ImGui.Render();
                cl.PushDebugGroup("ImGuiRenderer");
                RenderImDrawData(ImGui.GetDrawData(), gd, cl);
                cl.PopDebugGroup();
            }
        }

        /// <summary>
        /// This render ImGui and update the state.
        /// </summary>
        public void BeginEventUpdate(float deltaSeconds)
        {
            if (_frameBegun)
            {
                ImGui.Render();
            }

            SetPerFrameImGuiData(deltaSeconds);
        }

        public void UseWith(SdlWindow window)
        {
            window.EventFilter += HandleEvent;
            window.OnResized += HandleResized;
        }

        private unsafe bool HandleEvent(SdlWindow window, Event ev)
        {
            var io = ImGui.GetIO();
            switch((EventType)ev.Type)
            {
                case EventType.Mousemotion when ev.Motion.WindowID == window.WindowID:
                    io.AddMousePosEvent(ev.Motion.X, ev.Motion.Y);
                    return true;

                case EventType.Mousebuttondown or EventType.Mousebuttonup when ev.Motion.WindowID == window.WindowID:
                    var button = (MouseButton)ev.Button.Button switch
                    {
                        MouseButton.Left => ImGuiMouseButton.Left,
                        MouseButton.Middle => ImGuiMouseButton.Middle,
                        MouseButton.Right => ImGuiMouseButton.Right,
                        _ => ImGuiMouseButton.COUNT
                    };
                    if (button != ImGuiMouseButton.COUNT)
                        io.AddMouseButtonEvent((int)button, ev.Type == (uint)EventType.Mousebuttondown);
                    return button != ImGuiMouseButton.COUNT;

                case EventType.Mousewheel when ev.Wheel.WindowID == window.WindowID:
                    io.AddMouseWheelEvent(ev.Wheel.PreciseX, ev.Wheel.PreciseY);
                    return true;

                case EventType.Keydown or EventType.Keyup when ev.Key.WindowID == window.WindowID:
                    if (!TryMapKey((KeyCode)ev.Key.Keysym.Sym, out var imguiKey))
                        return false;
                    io.AddKeyEvent(imguiKey, ev.Type == (uint)EventType.Keydown);
                    return io.WantTextInput;

                case EventType.Textinput when ev.Text.WindowID == window.WindowID:
                    ImGuiNative.ImGuiIO_AddInputCharactersUTF8(io.NativePtr, ev.Text.Text);
                    return io.WantTextInput;
            }
            return false;
        }

        private void HandleResized(int width, int height)
        {
            _windowWidth = width;
            _windowHeight = height;
        }

        /// <summary>
        /// Called at the end of <see cref="Update(float)"/>.
        /// This tells ImGui that we are on the next frame.
        /// </summary>
        public void EndEventUpdate()
        {
            _frameBegun = true;
            ImGui.NewFrame();
        }

        /// <summary>
        /// Sets per-frame data based on the associated window.
        /// This is called by Update(float).
        /// </summary>
        private unsafe void SetPerFrameImGuiData(float deltaSeconds)
        {
            ImGuiIOPtr io = ImGui.GetIO();
            io.DisplaySize = new Vector2(
                _windowWidth / _scaleFactor.X,
                _windowHeight / _scaleFactor.Y);
            io.DisplayFramebufferScale = _scaleFactor;
            io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
        }

        private bool TryMapKey(KeyCode key, out ImGuiKey result)
        {
            ImGuiKey keyToImGuiKeyShortcut(KeyCode keyToConvert, KeyCode startKey1, ImGuiKey startKey2)
            {
                int changeFromStart1 = (int)keyToConvert - (int)startKey1;
                return startKey2 + changeFromStart1;
            }

            if (key >= KeyCode.KF1 && key <= KeyCode.KF12)
            {
                result = keyToImGuiKeyShortcut(key, KeyCode.KF1, ImGuiKey.F1);
                return true;
            }
            else if (key >= KeyCode.KKP0 && key <= KeyCode.KKP9)
            {
                result = keyToImGuiKeyShortcut(key, KeyCode.KKP0, ImGuiKey.Keypad0);
                return true;
            }
            else if (key >= KeyCode.KA && key <= KeyCode.KZ)
            {
                result = keyToImGuiKeyShortcut(key, KeyCode.KA, ImGuiKey.A);
                return true;
            }
            else if (key >= KeyCode.K0 && key <= KeyCode.K9)
            {
                result = keyToImGuiKeyShortcut(key, KeyCode.K0, ImGuiKey._0);
                return true;
            }

            switch (key)
            {
                case KeyCode.KLshift:
                case KeyCode.KRshift:
                    result = ImGuiKey.ModShift;
                    return true;
                case KeyCode.KLctrl:
                case KeyCode.KRctrl:
                    result = ImGuiKey.ModCtrl;
                    return true;
                case KeyCode.KLalt:
                case KeyCode.KRalt:
                    result = ImGuiKey.ModAlt;
                    return true;
                case KeyCode.KLgui:
                case KeyCode.KRgui:
                    result = ImGuiKey.ModSuper;
                    return true;
                case KeyCode.KMenu:
                    result = ImGuiKey.Menu;
                    return true;
                case KeyCode.KUp:
                    result = ImGuiKey.UpArrow;
                    return true;
                case KeyCode.KDown:
                    result = ImGuiKey.DownArrow;
                    return true;
                case KeyCode.KLeft:
                    result = ImGuiKey.LeftArrow;
                    return true;
                case KeyCode.KRight:
                    result = ImGuiKey.RightArrow;
                    return true;
                case KeyCode.KReturn:
                    result = ImGuiKey.Enter;
                    return true;
                case KeyCode.KEscape:
                    result = ImGuiKey.Escape;
                    return true;
                case KeyCode.KSpace:
                    result = ImGuiKey.Space;
                    return true;
                case KeyCode.KTab:
                    result = ImGuiKey.Tab;
                    return true;
                case KeyCode.KBackspace:
                    result = ImGuiKey.Backspace;
                    return true;
                case KeyCode.KInsert:
                    result = ImGuiKey.Insert;
                    return true;
                case KeyCode.KDelete:
                    result = ImGuiKey.Delete;
                    return true;
                case KeyCode.KPageup:
                    result = ImGuiKey.PageUp;
                    return true;
                case KeyCode.KPagedown:
                    result = ImGuiKey.PageDown;
                    return true;
                case KeyCode.KHome:
                    result = ImGuiKey.Home;
                    return true;
                case KeyCode.KEnd:
                    result = ImGuiKey.End;
                    return true;
                case KeyCode.KCapslock:
                    result = ImGuiKey.CapsLock;
                    return true;
                case KeyCode.KScrolllock:
                    result = ImGuiKey.ScrollLock;
                    return true;
                // Let's just not use PrintScreen for ImGui purposes and let it be used for RenderDoc
                //case KeyCode.KPrintscreen:
                //    result = ImGuiKey.PrintScreen;
                //    return true;
                case KeyCode.KPause:
                    result = ImGuiKey.Pause;
                    return true;
                case KeyCode.KNumlockclear:
                    result = ImGuiKey.NumLock;
                    return true;
                case KeyCode.KKPDivide:
                    result = ImGuiKey.KeypadDivide;
                    return true;
                case KeyCode.KKPMultiply:
                    result = ImGuiKey.KeypadMultiply;
                    return true;
                case KeyCode.KKPMinus:
                    result = ImGuiKey.KeypadSubtract;
                    return true;
                case KeyCode.KKPPlus:
                    result = ImGuiKey.KeypadAdd;
                    return true;
                case KeyCode.KKPDecimal:
                    result = ImGuiKey.KeypadDecimal;
                    return true;
                case KeyCode.KKPEnter:
                    result = ImGuiKey.KeypadEnter;
                    return true;
                case KeyCode.KBackquote:
                    result = ImGuiKey.GraveAccent;
                    return true;
                case KeyCode.KMinus:
                    result = ImGuiKey.Minus;
                    return true;
                case KeyCode.KPlus:
                    result = ImGuiKey.Equal;
                    return true;
                case KeyCode.KLeftbracket:
                    result = ImGuiKey.LeftBracket;
                    return true;
                case KeyCode.KRightbracket:
                    result = ImGuiKey.RightBracket;
                    return true;
                case KeyCode.KSemicolon:
                    result = ImGuiKey.Semicolon;
                    return true;
                case KeyCode.KQuote:
                    result = ImGuiKey.Apostrophe;
                    return true;
                case KeyCode.KComma:
                    result = ImGuiKey.Comma;
                    return true;
                case KeyCode.KPeriod:
                    result = ImGuiKey.Period;
                    return true;
                case KeyCode.KSlash:
                    result = ImGuiKey.Slash;
                    return true;
                case KeyCode.KBackslash:
                    result = ImGuiKey.Backslash;
                    return true;
                default:
                    result = ImGuiKey.GamepadBack;
                    return false;
            }
        }

        private unsafe void RenderImDrawData(ImDrawDataPtr draw_data, GraphicsDevice gd, CommandList cl)
        {
            uint vertexOffsetInVertices = 0;
            uint indexOffsetInElements = 0;

            if (draw_data.CmdListsCount == 0)
            {
                return;
            }

            uint totalVBSize = (uint)(draw_data.TotalVtxCount * sizeof(ImDrawVert));
            if (totalVBSize > _vertexBuffer.SizeInBytes)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalVBSize * 1.5f), BufferUsage.VertexBuffer | BufferUsage.Dynamic));
                _vertexBuffer.Name = $"ImGui.NET Vertex Buffer";
            }

            uint totalIBSize = (uint)(draw_data.TotalIdxCount * sizeof(ushort));
            if (totalIBSize > _indexBuffer.SizeInBytes)
            {
                _indexBuffer.Dispose();
                _indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalIBSize * 1.5f), BufferUsage.IndexBuffer | BufferUsage.Dynamic));
                _indexBuffer.Name = $"ImGui.NET Index Buffer";
            }

            for (int i = 0; i < draw_data.CmdListsCount; i++)
            {
                ImDrawListPtr cmd_list = ((ImDrawList**)draw_data.CmdLists)[i];

                cl.UpdateBuffer(
                    _vertexBuffer,
                    vertexOffsetInVertices * (uint)sizeof(ImDrawVert),
                    cmd_list.VtxBuffer.Data,
                    (uint)(cmd_list.VtxBuffer.Size * sizeof(ImDrawVert)));

                cl.UpdateBuffer(
                    _indexBuffer,
                    indexOffsetInElements * sizeof(ushort),
                    cmd_list.IdxBuffer.Data,
                    (uint)(cmd_list.IdxBuffer.Size * sizeof(ushort)));

                vertexOffsetInVertices += (uint)cmd_list.VtxBuffer.Size;
                indexOffsetInElements += (uint)cmd_list.IdxBuffer.Size;
            }

            // Setup orthographic projection matrix into our constant buffer
            {
                var io = ImGui.GetIO();

                Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
                    0f,
                    io.DisplaySize.X,
                    io.DisplaySize.Y,
                    0.0f,
                    -1.0f,
                    1.0f);

                _gd.UpdateBuffer(_projMatrixBuffer, 0, ref mvp);
            }

            cl.SetVertexBuffer(0, _vertexBuffer);
            cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            cl.SetPipeline(_pipeline);
            cl.SetGraphicsResourceSet(0, _mainResourceSet);

            draw_data.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

            // Render command lists
            int vtx_offset = 0;
            int idx_offset = 0;
            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                ImDrawListPtr cmd_list = ((ImDrawList**)draw_data.CmdLists)[n];
                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
                {
                    ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        if (pcmd.TextureId != IntPtr.Zero)
                        {
                            if (pcmd.TextureId == _fontAtlasID)
                            {
                                cl.SetGraphicsResourceSet(1, _fontTextureResourceSet);
                            }
                            else
                            {
                                cl.SetGraphicsResourceSet(1, GetImageResourceSet(pcmd.TextureId));
                            }
                        }

                        float clipRectX = Math.Max(0, pcmd.ClipRect.X); // Vulkan validation requires positive scissor offsets
                        float clipRectY = Math.Max(0, pcmd.ClipRect.Y);
                        cl.SetScissorRect(
                            0,
                            (uint)clipRectX,
                            (uint)clipRectY,
                            (uint)(pcmd.ClipRect.Z - clipRectX),
                            (uint)(pcmd.ClipRect.W - clipRectY));

                        cl.DrawIndexed(pcmd.ElemCount, 1, pcmd.IdxOffset + (uint)idx_offset, (int)(pcmd.VtxOffset + vtx_offset), 0);
                    }
                }

                idx_offset += cmd_list.IdxBuffer.Size;
                vtx_offset += cmd_list.VtxBuffer.Size;
            }
        }

        /// <summary>
        /// Frees all graphics resources used by the renderer.
        /// </summary>
        public void Dispose()
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
            _projMatrixBuffer.Dispose();
            _fontTexture.Dispose();
            _vertexShader.Dispose();
            _fragmentShader.Dispose();
            _layout.Dispose();
            _textureLayout.Dispose();
            _pipeline.Dispose();
            _mainResourceSet.Dispose();
            _fontTextureResourceSet.Dispose();

            foreach (IDisposable resource in _ownedResources)
            {
                resource.Dispose();
            }
        }

        private struct ResourceSetInfo
        {
            public readonly IntPtr ImGuiBinding;
            public readonly ResourceSet ResourceSet;

            public ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet)
            {
                ImGuiBinding = imGuiBinding;
                ResourceSet = resourceSet;
            }
        }
    }
}
