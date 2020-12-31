using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzre.core;
using zzre.imgui;
using zzre.rendering;
using System.Linq;

namespace zzre.tools
{
    public class TestRaycaster : ListDisposable
    {
        private class RaycastObject
        {
            public IRaycastable Geometry { get; init; } = null!;
            public IColor Color { get; init; } = IColor.White;
            public Func<RaycastObject, Vector3, Raycast, IColor> Shader { get; init; } = (_1, _2, _3) => IColor.Black;
        }

        private readonly ITagContainer diContainer;
        private readonly FramebufferArea fbArea;
        private readonly MouseEventArea mouseEventArea;
        private readonly Camera camera;
        private readonly FlyControlsTag controls;
        private readonly GraphicsDevice device;
        private readonly IReadOnlyList<RaycastObject> objects;

        private IColor[]? pixels;
        private int PixelCount => (int)(fbArea.Framebuffer.Width * fbArea.Framebuffer.Height);

        public Window Window { get; }

        public TestRaycaster(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            device = diContainer.GetTag<GraphicsDevice>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Test Raycaster");
            Window.AddTag(this);
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 200f, 200f);

            var localDiContainer = diContainer
                .ExtendedWith(new LocationBuffer(device));
            localDiContainer.AddTag(camera = new Camera(localDiContainer));
            fbArea = new FramebufferArea(Window, device);
            mouseEventArea = new MouseEventArea(Window);
            controls = new FlyControlsTag(Window, camera.Location, localDiContainer);
            AddDisposable(camera);

            fbArea.OnResize += OnResize;
            Window.OnRender += OnRender; // on window to not update framebuffer texture during rendering
            Window.OnContent += OnContent;

            objects = new[]
            {
                new RaycastObject()
                {
                    Geometry = new Sphere(Vector3.UnitZ * -3f, 1f),
                    Shader = ShaderNormal
                },
                new RaycastObject()
                {
                    Geometry = new Plane(Vector3.UnitY * -1f, -1f),
                    Shader = ShaderChecker
                },
                new RaycastObject()
                {
                    Geometry = new Triangle(
                        new Vector3(-2f, 0f, -0.5f),
                        new Vector3(-2f, 1f, 0f),
                        new Vector3(-2f, 0f, +0.5f)),
                    Shader = ShaderBarycentric
                },
                new RaycastObject()
                {
                    Geometry = new Box(Vector3.UnitZ * 3f, Vector3.One),
                    Shader = ShaderNormal
                }
            };
        }

        private void OnContent()
        {
            mouseEventArea.Content();
            fbArea.Content();
        }

        private void OnResize()
        {
            camera.Aspect = ((float)fbArea.Framebuffer.Width) / fbArea.Framebuffer.Height;

            if (PixelCount <= (pixels?.Length ?? 0))
                return;
            pixels = new IColor[PixelCount];
        }

        private void OnRender()
        {
            if (pixels == null)
                OnResize();

            if (!Matrix4x4.Invert(camera.Projection, out var invProj))
                throw new InvalidProgramException("Could not invert camera projection");

            Parallel.For(0, PixelCount, i =>
            {
                int pixelX = i % (int)fbArea.Framebuffer.Width;
                int pixelY = i / (int)fbArea.Framebuffer.Width;
                var pixelPos = new Vector3(
                    pixelX / (float)fbArea.Framebuffer.Width,
                    pixelY / (float)fbArea.Framebuffer.Height,
                    1.0f) * 2f - Vector3.One;
                var _projected = Vector3.Transform(pixelPos, invProj);
                var projected = Vector3.Transform(_projected, camera.Location.LocalToWorld);
                var ray = new Ray(
                    camera.Location.GlobalPosition,
                    Vector3.Normalize(projected - camera.Location.GlobalPosition));

                var (cast, obj) = objects
                    .Select(obj => (cast: obj.Geometry.Cast(ray), obj))
                    .OrderBy(t => t.cast?.Distance ?? float.MaxValue)
                    .FirstOrDefault();
                pixels![i] = cast.HasValue
                    ? obj.Shader(obj, pixelPos, cast.Value)
                    : IColor.Black;
            });

            device.UpdateTexture(fbArea.Framebuffer.ColorTargets.First().Target, pixels, 0, 0, 0,
                fbArea.Framebuffer.Width, fbArea.Framebuffer.Height, 1,
                0, 0);
        }

        private IColor ShaderSolid(RaycastObject obj, Vector3 _1, Raycast _2) => obj.Color;

        private IColor ShaderNormal(RaycastObject obj, Vector3 _1, Raycast r) => new IColor(
            (byte)((r.Normal.X + 1f) * 127f),
            (byte)((r.Normal.Y + 1f) * 127f),
            (byte)((r.Normal.Z + 1f) * 127f),
            255);

        private IColor ShaderChecker(RaycastObject obj, Vector3 _1, Raycast r)
        {
            int Check(float p) => Math.Abs(((int)Math.Round(p)) % 2);
            return Check(r.Point.X) == Check(r.Point.Z) ? IColor.Red : IColor.Blue;
        }

        private IColor ShaderDistance(RaycastObject obj, Vector3 _1, Raycast r)
        {
            if (r.Distance < 0f)
                return new IColor(255, 0, 255, 255);
            byte d = (byte)Math.Clamp(255.0f - r.Distance, 0, 255f);
            return new IColor(d, d, d, 255);
        }

        private IColor ShaderBarycentric(RaycastObject obj, Vector3 _1, Raycast r)
        {
            if (obj.Geometry is not Triangle)
                return new IColor(255, 0, 255, 255);
            var triangle = (Triangle)obj.Geometry;
            var bary = triangle.Barycentric(r.Point);

            byte Color(float p) => (byte)(p * 255f);
            return new IColor(Color(bary.X), Color(bary.Y), Color(bary.Z), 255);
        }
    }
}
