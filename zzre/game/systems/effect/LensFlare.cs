using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Serilog;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems.effect;

public partial class LensFlare : AEntitySetSystem<float>
{
    private const float CanonicalRatio = 1024f / 768f;
    private static readonly EffectMaterialAsset.Info MaterialInfo = new(
        TextureName: "lsf000t",
        EffectMaterial.BillboardMode.LensFlare,
        EffectMaterial.BlendMode.AdditiveAlpha,
        DepthTest: false,
        AlphaReference: 0.0196078f,
        HasFog: false);

    [Configuration]
    private float PrimarySizeFactor = 0.7f;
    [Configuration]
    private float PrimaryAngleFactor = 4f;
    [Configuration]
    private float DistanceAlphaFactor = 1 / 30f;
    [Configuration]
    private float AngleAlphaMin = 0.75f;
    [Configuration]
    private float AngleAlphaMax = 0.9f;
    [Configuration]
    private float AngleAlphaFactor = 20 / 3f;
    [Configuration]
    private float AlphaSpeed = 2f;

    private readonly record struct Flare(float OffsetFactor, float Size, int TileI);
    private static readonly IReadOnlyList<IReadOnlyList<Flare>> FlareInfos = new IReadOnlyList<Flare>[]
    {
        new Flare[]
        {
            new(1f, 1f, 1),
            new(1.2f, 0.1f, 2),
            new(0.5f, 0.12f, 7),
            new(0.1f, 0.08f,  3),
            new(-0.3f, 0.07f, 2),
            new(-0.5f, 0.12f, 4),
            new(-0.9f, 0.4f, 0),
            new(0.3f, 0.05f, 2),
            new(-0.05f, 0.03f, 1),
            new(-0.4f, 0.04f, 1)
        },
        new Flare[]
        {
            new(1f, 1f, 1),
            new(0.5f, 0.1f, 3),
            new(0.25f, 0.3f, 2),
            new(0.15f, 0.25f, 2),
            new(0.05f, 0.2f, 2),
        },
        new Flare[]
        {
            new(1f, 1f, 1),
            new(0.5f, 0.1f, 3),
            new(0.25f, 0.3f, 2)
        },
        new Flare[] {new Flare(1f, 0.4f, 4) },
        new Flare[] {new Flare(1f, 0.4f, 5) },
        new Flare[] {new Flare(1f, 0.4f, 8) },
        new Flare[] {new Flare(1f, 0.4f, 9) }
    };

    private readonly ILogger logger;
    private readonly Camera camera;
    private readonly IZanzarahContainer zzContainer;
    private readonly IAssetRegistry assetRegistry;
    private readonly EffectMesh effectMesh;
    private readonly IDisposable sceneChangingDisposable;
    private readonly IDisposable sceneLoadedDisposable;
    private readonly IDisposable componentRemovedDisposable;
    private WorldCollider? worldCollider;
    private WorldMesh? worldMesh;
    private Vector2 screenSizeFactor;

    public LensFlare(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        logger = diContainer.GetTag<ILogger>();
        camera = diContainer.GetTag<Camera>();
        zzContainer = diContainer.GetTag<IZanzarahContainer>();
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        effectMesh = diContainer.GetTag<EffectMesh>();
        zzContainer.OnResize += HandleResize;
        sceneChangingDisposable = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadedDisposable = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        componentRemovedDisposable = World.SubscribeEntityComponentRemoved<components.effect.LensFlare>(HandleComponentRemoved);
        HandleResize();
    }

    public override void Dispose()
    {
        base.Dispose();
        zzContainer.OnResize -= HandleResize;
        sceneChangingDisposable.Dispose();
        sceneLoadedDisposable.Dispose();
        componentRemovedDisposable.Dispose();
    }

    private void HandleResize()
    {
        float w = zzContainer.Framebuffer.Width;
        float h = zzContainer.Framebuffer.Height;
        var curRatio = w / h;
        screenSizeFactor.X = CanonicalRatio / curRatio;
        screenSizeFactor.Y = screenSizeFactor.X * curRatio;
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => Set.DisposeAll();

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        worldCollider = World.Get<WorldCollider>();
        worldMesh = World.Get<WorldMesh>();

        foreach (var trigger in msg.Scene.triggers.Where(t => t.type == zzio.scn.TriggerType.LensFlare))
        {
            if (trigger.ii1 > FlareInfos.Count)
            {
                logger.Warning("Ignored LensFlare with type {Type}", trigger.ii1);
                continue;
            }

            var entity = World.CreateEntity();
            entity.Set(new Location()
            {
                LocalPosition = trigger.pos
            });
            assetRegistry.LoadEffectMaterial(entity, MaterialInfo);
            var vertexRange = effectMesh.RentVertices(4 * FlareInfos[(int)trigger.ii1].Count);
            var indexRange = effectMesh.RentQuadIndices(vertexRange);
            entity.Set(new components.effect.RenderIndices(indexRange));
            entity.Set(components.RenderOrder.LateEffect);
            entity.Set(new components.effect.LensFlare(
                vertexRange,
                indexRange,
                (int)trigger.ii1));

            var attrColor = effectMesh.Color.Write(vertexRange);
            attrColor.Fill(new zzio.IColor((byte)trigger.ii2, (byte)trigger.ii3, (byte)trigger.ii4, 255));

            var attrUV = effectMesh.UV.Write(vertexRange);
            for (int i = 0; i < FlareInfos[(int)trigger.ii1].Count; i++)
            {
                var texCoords = GetTexCoords(FlareInfos[(int)trigger.ii1][i].TileI);
                attrUV[i * 4 + 0] = new(texCoords.Min.X, texCoords.Min.Y);
                attrUV[i * 4 + 1] = new(texCoords.Min.X, texCoords.Max.Y);
                attrUV[i * 4 + 2] = new(texCoords.Max.X, texCoords.Max.Y);
                attrUV[i * 4 + 3] = new(texCoords.Max.X, texCoords.Min.Y);
            }
        }
    }

    private void HandleComponentRemoved(in DefaultEcs.Entity entity, in components.effect.LensFlare flare)
    {
        effectMesh.ReturnVertices(flare.VertexRange);
        effectMesh.ReturnIndices(flare.IndexRange);
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        Location location,
        ref components.effect.LensFlare flare)
    {
        var nearClipPos = camera.Location.LocalPosition - camera.Location.InnerForward * camera.NearPlane * 1.1f;
        var camToMe = MathEx.SafeNormalize(location.LocalPosition - nearClipPos, Vector3.UnitY);
        var cosCamAngle = Vector3.Dot(camToMe, -camera.Location.InnerForward);
        if (camera.TransformWorldPoint(location.LocalPosition) is not Vector3 screenPos)
        {
            entity.Remove<components.Visibility>();
            return;
        }

        float targetAlpha = flare.Type switch
        {
            _ when IsBlockedByWorld(location.LocalPosition) => 0f,
            _ when flare.Type is >= 3 and <= 6 => 1f - location.Distance(camera.Location) * DistanceAlphaFactor,
            _ when cosCamAngle < AngleAlphaMin => 0f,
            _ when cosCamAngle > AngleAlphaMax => 1f,
            _ => (cosCamAngle - AngleAlphaMin) * AngleAlphaFactor
        };
        flare.CurAlpha = MathEx.Lerp(flare.CurAlpha, targetAlpha, elapsedTime * AlphaSpeed);
        if (flare.CurAlpha <= 0f)
        {
            flare.CurAlpha = 0f;
            entity.Remove<components.Visibility>();
            return;
        }
        flare.CurAlpha = Math.Min(flare.CurAlpha, 1f);
        var attrColor = effectMesh.Color.Write(flare.VertexRange);
        attrColor.Fill(attrColor[0] with { a = (byte)(flare.CurAlpha * 255) });

        var flareInfos = FlareInfos[flare.Type];
        var attrCenter = effectMesh.Center.Write(flare.VertexRange);
        for (int i = 0; i < flareInfos.Count; i++)
            attrCenter.Slice(i * 4, 4).Fill((screenPos * flareInfos[i].OffsetFactor) with { Z = 0.5f });

        var attrPos = effectMesh.Pos.Write(flare.VertexRange);
        SetPrimaryPos(attrPos, flareInfos[0].Size, cosCamAngle);
        for (int i = 1; i < flareInfos.Count; i++)
            SetSecondaryPos(attrPos[(i * 4)..], flareInfos[i].Size);

        entity.Set<components.Visibility>();
    }

    private bool IsBlockedByWorld(Vector3 worldPos)
    {
        if (worldCollider is null || worldMesh is null)
            return false;

        var line = new Line(camera.Location.LocalPosition, worldPos);
        return worldCollider.Intersections(line).Any(intersection =>
        {
            if (intersection.TriangleId == null)
                return false;
            var info = worldCollider.GetTriangleInfo(intersection.TriangleId.Value);
            var rwMaterial = worldMesh.Materials[(int)info.Atomic.matIdBase + info.VertexTriangle.m];
            var rwTexture = rwMaterial.FindChildById(SectionId.Texture, recursive: false) as RWTexture;
            var rwTextureName = rwTexture?.FindChildById(SectionId.String, recursive: false) as RWString;
            var rwMaskTextureName = rwTexture?.FindAllChildrenById(SectionId.String, recursive: false).ElementAtOrDefault(1) as RWString;
            if (rwTextureName?.value is null ||
                rwTextureName.value.StartsWith('_') ||
                rwMaskTextureName?.value is null || // this is IsNullOrNotEmpty
                rwMaskTextureName?.value.Length > 0) // which string does not have
                return false;
            return true;
        });
    }

    private void SetPrimaryPos(Span<Vector3> vertices, float flareSize, float cosCamAngle)
    {
        var angle = MathF.Acos(cosCamAngle) * PrimaryAngleFactor;
        float c, s;
        c = MathF.Cos(angle) * flareSize * PrimarySizeFactor;
        s = MathF.Sin(angle) * flareSize * PrimarySizeFactor;
        vertices[0] = new(-c, -s, 0f);
        vertices[1] = new(-s, +c, 0f);
        vertices[2] = new(+c, +s, 0f);
        vertices[3] = new(+s, -c, 0f);
        vertices[0] *= new Vector3(screenSizeFactor, 0f);
        vertices[1] *= new Vector3(screenSizeFactor, 0f);
        vertices[2] *= new Vector3(screenSizeFactor, 0f);
        vertices[3] *= new Vector3(screenSizeFactor, 0f);
    }

    private void SetSecondaryPos(Span<Vector3> vertices, float flareSize)
    {
        var s = flareSize * screenSizeFactor;
        vertices[0] = new(-s.X, -s.Y, 0f);
        vertices[1] = new(-s.X, +s.Y, 0f);
        vertices[2] = new(+s.X, +s.Y, 0f);
        vertices[3] = new(+s.X, -s.Y, 0f);
    }

    private static Rect GetTexCoords(int tileI)
    {
        if (tileI < 2)
            return Rect.FromTopLeftSize(Vector2.UnitX * tileI * 0.5f, Vector2.One * 0.5f);
        tileI -= 2;
        return Rect.FromTopLeftSize(new((tileI % 4) * 0.25f, 0.5f + (tileI / 4) * 0.25f), Vector2.One * 0.25f);
    }
}
