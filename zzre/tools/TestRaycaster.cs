using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Veldrid;
using zzre.imgui;
using zzre.rendering;
using System.Linq;
using Quaternion = System.Numerics.Quaternion;
using zzio.vfs;
using zzio.rwbs;
using zzio;

namespace zzre.tools;

public class TestRaycaster : ListDisposable
{
    private class RaycastObject
    {
        public IRaycastable Geometry { get; set; } = null!;
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
    private readonly RaycastObject rotatingBox;

    private IColor[]? pixels;
    private int PixelCount => (int)(fbArea.Framebuffer.Width * fbArea.Framebuffer.Height);
    private float rotation = 0.0f;

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

        var rwWorld = diContainer.GetTag<IResourcePool>().FindFile("resources/worlds/sc_3302.bsp")!.OpenAsRWBS<RWWorld>();
        var worldCollider = new WorldCollider(rwWorld);
        camera.Location.LocalPosition = -rwWorld.origin;

        rotatingBox = new RaycastObject()
        {
            Geometry = new OrientedBox(new Box(Vector3.UnitX * 3f, Vector3.One), Quaternion.Identity),
            Shader = ShaderNormal
        };
        objects = new[]
        {
            /*new RaycastObject()
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
            },
            rotatingBox,*/
            new RaycastObject()
            {
                Geometry = worldCollider,
                Shader = ShaderMaterialIndex
            }
        };
    }

    private void OnContent()
    {
        mouseEventArea.Content();
        fbArea.Content();

        rotation += 3.0f / 180f * MathF.PI;
        var loc = new Location
        {
            LocalPosition =
            Vector3.UnitX * (3f + MathF.Sin(rotation)) +
            Vector3.UnitY * MathF.Cos(rotation) * 0.5f +
            Vector3.UnitZ * MathF.Cos(rotation),
            LocalRotation = Quaternion.CreateFromYawPitchRoll(rotation, rotation, 0f)
        };
        rotatingBox.Geometry = new Box(Vector3.Zero, new Vector3(0.4f, 0.8f, 1.2f)).TransformToWorld(loc);
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

        int width = (int)fbArea.Framebuffer.Width;
        int height = (int)fbArea.Framebuffer.Height;
        //for (int i = 0; i < PixelCount; i++)
        Parallel.For(0, PixelCount, i =>
        {
            int pixelX = i % width;
            int pixelY = height - 1 - i / width;
            var pixelPos = new Vector3(
                pixelX / (float)width,
                pixelY / (float)height,
                1.0f) * 2f - Vector3.One;
            var ray = camera.RayAt(new Vector2(pixelPos.X, pixelPos.Y));

            var bestCast = (cast: null as Raycast?, obj: null as RaycastObject);
            foreach (var newObj in objects)
            {
                var newCast = newObj.Geometry.Cast(ray);
                if (bestCast.cast == null || newCast != null && newCast.Value.Distance < bestCast.cast.Value.Distance)
                    bestCast = (newCast, newObj);
            }
            var (cast, obj) = bestCast;
            pixels![i] = cast.HasValue
                ? obj!.Shader(obj, pixelPos, cast.Value)
                : IColor.Black;
        });

        device.UpdateTexture(fbArea.Framebuffer.ColorTargets.First().Target, pixels, 0, 0, 0,
            fbArea.Framebuffer.Width, fbArea.Framebuffer.Height, 1,
            0, 0);
    }

    private IColor ShaderSolid(RaycastObject obj, Vector3 _1, Raycast _2) => obj.Color;

    private IColor ShaderNormal(RaycastObject obj, Vector3 _1, Raycast r) => new(
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

    private IColor ShaderMaterialIndex(RaycastObject obj, Vector3 _1, Raycast r)
    {
        int m = r.VertexTriangle?.m ?? 0;
        byte d = (byte)(m * 255 / 31);
        return new IColor(d, d, d, 25);
    }
}
