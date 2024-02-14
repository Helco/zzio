using System;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.game.systems;

// this class exists mostly to put it into the SequentialSystem so we can
// manage the render order more easily

public class WorldRendererSystem : ISystem<CommandList>
{
    private readonly ITagContainer diContainer;
    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable sceneLoadedSubscription;
    private WorldRenderer? renderer;

    public bool IsEnabled { get; set; } = true;

    public WorldRendererSystem(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ecsWorld.SetMaxCapacity<WorldMesh>(1);
        ecsWorld.SetMaxCapacity<WorldCollider>(1);
        ecsWorld.SetMaxCapacity<WorldRenderer>(1);
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public void Dispose()
    {
        renderer?.WorldMesh?.Dispose();
        renderer?.Dispose();
        sceneLoadedSubscription.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        renderer?.WorldMesh?.Dispose();
        renderer?.Dispose();

        var scene = message.Scene;
        var worldPath = new FilePath("resources").Combine(scene.misc.worldPath, scene.misc.worldFile + ".bsp");
        var worldMesh = new WorldMesh(diContainer, worldPath);

        ecsWorld.Set(worldMesh);
        ecsWorld.Set(new WorldCollider(worldMesh.World));
        ecsWorld.Set(renderer = new WorldRenderer(diContainer));
        renderer.WorldMesh = worldMesh;
    }

    public void Update(CommandList cl)
    {
        if (!IsEnabled || renderer is null)
            return;
        renderer.UpdateVisibility();
        renderer.Render(cl);
    }
}
