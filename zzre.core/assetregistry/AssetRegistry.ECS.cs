namespace zzre;

partial class AssetRegistry
{
    public static void SubscribeAt(DefaultEcs.World world)
    {
        world.SubscribeEntityComponentRemoved<AssetHandle>(HandleAssetHandleRemoved);
        world.SubscribeWorldDisposed(HandleWorldDisposed);
    }

    private static void HandleAssetHandleRemoved(in DefaultEcs.Entity entity, in AssetHandle handle) =>
        handle.Dispose();

    private static void HandleWorldDisposed(DefaultEcs.World world)
    {
        var assetHandleEntities = world.GetEntities()
            .With<AssetHandle>()
            .AsEnumerable();
        var assetHandles = world.GetComponents<AssetHandle>();
        foreach (var entity in assetHandleEntities)
            assetHandles[entity].Dispose();
    }
}
