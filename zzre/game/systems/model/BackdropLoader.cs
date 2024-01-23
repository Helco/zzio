using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using DefaultEcs.Resource;
using zzre.rendering;
using System.Numerics;
using zzio;

namespace zzre.game.systems;

public class BackdropLoader : ISystem<float>
{
    private readonly Camera camera;
    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable sceneLoadSubscription;

    public bool IsEnabled { get; set; } = true;

    public BackdropLoader(ITagContainer diContainer)
    {
        camera = diContainer.GetTag<Camera>();
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
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

        if (char.IsDigit(backdropName.First()))
        {
            Console.WriteLine("Warning: Unsupported dynamic backdrop " + backdropName);
        }
        else
            CreateStaticBackdrop(backdropName);
    }

    private DefaultEcs.Entity CreateStaticBackdrop(string name)
    {
        var entity = ecsWorld.CreateEntity();
        entity.Set(new Location());
        entity.Set(new components.MoveToLocation(camera.Location, RelativePosition: Vector3.Zero));
        entity.Set(ManagedResource<ClumpMesh>.Create(resources.ClumpInfo.Backdrop(name + ".dff")));
        entity.Set(components.Visibility.Visible);
        entity.Set(components.RenderOrder.Backdrop);
        entity.Set(new components.ClumpMaterialInfo()
        {
            Color = IColor.White
        });

        var clumpMesh = entity.Get<ClumpMesh>();
        entity.Set(new List<materials.ModelMaterial>(clumpMesh.Materials.Count));
        entity.Set(ManagedResource<materials.ModelMaterial>.Create(clumpMesh.Materials
            .Select(rwMaterial => new resources.ClumpMaterialInfo(zzio.scn.FOModelRenderType.Solid, rwMaterial))
            .ToArray()));

        return entity;
    }
}
