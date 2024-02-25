using System;
using System.Linq;
using DefaultEcs.System;
using zzre.rendering;
using System.Numerics;
using zzio;
using Serilog;

namespace zzre.game.systems;

public class BackdropLoader : ISystem<float>
{
    private readonly ILogger logger;
    private readonly Camera camera;
    private readonly DefaultEcs.World ecsWorld;
    private readonly IAssetRegistry assetRegistry;
    private readonly IDisposable sceneLoadSubscription;

    public bool IsEnabled { get; set; } = true;

    public BackdropLoader(ITagContainer diContainer)
    {
        logger = diContainer.GetLoggerFor<BackdropLoader>();
        camera = diContainer.GetTag<Camera>();
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        sceneLoadSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public void Dispose()
    {
        sceneLoadSubscription.Dispose();
    }

    public void Update(float state)
    {
    }

    // We do not have to care about scene changing, the ModelLoader will dispose
    // the static backdrop (ParentReaper handles the rest through Parent association)

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        if (!IsEnabled)
            return;
        var backdropName = message.Scene.backdropFile;
        if (string.IsNullOrWhiteSpace(backdropName))
            return;

        int? dynBackdropId = char.IsDigit(backdropName.First())
            ? int.Parse(backdropName[..backdropName.IndexOfAnyNot("0123456789".ToArray())])
            : null;
        switch(dynBackdropId)
        {
            case 2: // Forest
                CreateStaticBackdrop("ebg01h", depthTest: false, depthWrite: false);
                break;
            case 3: // Horizon
                CreateStaticBackdrop("sbg01m", hasFog: false).Set(new components.behaviour.Rotate(Vector3.UnitY, 1f));
                CreateStaticBackdrop("sbg02m", hasFog: false);
                break;
            case 5: // Swamp
                CreateStaticBackdrop("bgsm01p", depthTest: false, depthWrite: false,
                    rotation: Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / -2));
                break;
            case 7: // Mountain1
                CreateStaticBackdrop("msk01f", depthTest: false, depthWrite: false, hasFog: false);
                break;
            case 10: // Garden
                CreateStaticBackdrop("fbgsm01p", depthTest: false, depthWrite: false,
                    rotation: Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / -2));
                break;
            case null: CreateStaticBackdrop(backdropName); break;
            default: logger.Warning("Unsupported dynamic backdrop {Name}", backdropName); break;
        }
    }

    private DefaultEcs.Entity CreateStaticBackdrop(string name, bool depthTest = true, bool depthWrite = true, bool hasFog = true, Quaternion? rotation = null)
    {
        var materialVariant = new ClumpMaterialAsset.MaterialVariant(
            materials.ModelMaterial.BlendMode.Alpha,
            DepthTest: depthTest,
            DepthWrite: depthWrite,
            HasFog: hasFog);

        var entity = ecsWorld.CreateEntity();
        entity.Set(new Location()
        {
            LocalRotation = rotation ?? Quaternion.Identity
        });
        entity.Set(new components.MoveToLocation(camera.Location, RelativePosition: Vector3.Zero));
        entity.Set(components.Visibility.Visible);
        entity.Set(components.RenderOrder.Backdrop);
        entity.Set(new components.ClumpMaterialInfo()
        {
            Color = IColor.White
        });
        assetRegistry.LoadBackdrop(entity, name, AssetLoadPriority.Synchronous, materialVariant);

        return entity;
    }
}
