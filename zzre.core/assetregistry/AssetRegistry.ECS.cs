﻿namespace zzre;

partial class AssetRegistry
{
    /// <summary>
    /// Adds watchers to automatically dispose <see cref="AssetHandle"/> components when they are removed from an entity or the world is disposed
    /// </summary>
    /// <param name="world">The world to register at</param>
    public static void SubscribeAt(DefaultEcs.World world)
    {
        world.SubscribeEntityComponentRemoved<AssetHandle>(HandleAssetHandleRemoved);
        world.SubscribeEntityComponentRemoved<AssetHandle[]>(HandleAssetHandlesRemoved);
        world.SubscribeWorldDisposed(HandleWorldDisposed);
    }

    private static void HandleAssetHandleRemoved(in DefaultEcs.Entity entity, in AssetHandle handle) =>
        handle.Dispose();

    private static void HandleAssetHandlesRemoved(in DefaultEcs.Entity entity, in AssetHandle[] handles)
    {
        foreach (var handle in handles)
            handle.Dispose();
    }

    private static void HandleWorldDisposed(DefaultEcs.World world)
    {
        var assetHandleEntities = world.GetEntities()
            .With<AssetHandle>()
            .AsEnumerable();
        var assetHandles = world.GetComponents<AssetHandle>();
        foreach (var entity in assetHandleEntities)
            assetHandles[entity].Dispose();
        
        var assetHandleArrayEntities = world.GetEntities()
            .With<AssetHandle[]>()
            .AsEnumerable();
        var assetHandleArrays = world.GetComponents<AssetHandle[]>();
        foreach (var entity in assetHandleArrayEntities)
        {
            foreach (var handle in assetHandleArrays[entity])
                handle.Dispose();
        }
    }
}
