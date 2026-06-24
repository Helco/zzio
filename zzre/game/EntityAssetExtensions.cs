using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using zzre.materials;
using zzre.rendering;

namespace zzre.game;

public static class EntityAssetExtensions
{
    public static void SubscribeAt(this IAssetRegistry registry, DefaultEcs.World world)
    {
        world.SubscribeEntityDisposed(HandleEntityDisposed);
    }

    private static void HandleEntityDisposed(in Entity entity)
    {
        if (entity.TryGet<AssetHandle>(out var handle))
            handle.Dispose();
        if (entity.TryGet<AssetHandle[]>(out var handles))
        {
            foreach (var handle_ in handles)
                handle_.Dispose();
        }
    }

    public static Vector2 LoadUIBitmapFor(this IAssetRegistry registry, DefaultEcs.Entity entity,
        string bitmap, bool hasRawMask = false)
    {
        var handle = registry.LoadUIBitmap(bitmap, hasRawMask);
        var asset = handle.Get();
        entity.Set(handle.As());
        entity.Set(asset.Material);
        return asset.Size;
    }

    public static TileSheet LoadUITileSheetFor(this IAssetRegistry registry, DefaultEcs.Entity entity,
        in UITileSheetAsset.Info info)
    {
        var handle = registry.LoadUITileSheet(info);
        var asset = handle.Get();
        entity.Set(handle.As());
        entity.Set(asset.Material);
        entity.Set(asset.TileSheet);
        return asset.TileSheet;
    }

    public static TileSheet LoadUITileSheetFor(this IAssetRegistry registry, ref DefaultEcs.Command.EntityRecord entity,
        in UITileSheetAsset.Info info)
    {
        var handle = registry.LoadUITileSheet(info);
        var asset = handle.Get();
        entity.Set(handle.As());
        entity.Set(asset.Material);
        entity.Set(asset.TileSheet);
        return asset.TileSheet;
    }

    public static AssetHandle<ClumpAsset> LoadModelClumpAndMaterialFor(this IAssetRegistry registry, DefaultEcs.Entity entity,
        string modelName,
        ModelMaterial.Variant variant,
        StandardTextureKind placeholder,
        AssetPriority priority)
    {
        var clumpHandle = registry.LoadModelClump(modelName, priority);
        return LoadClumpMaterialFor(registry, entity, clumpHandle, variant, placeholder, priority);
    }

    public static AssetHandle<ClumpAsset> LoadBackdropClumpAndMaterialFor(this IAssetRegistry registry, DefaultEcs.Entity entity,
        string modelName,
        ModelMaterial.Variant variant,
        StandardTextureKind placeholder,
        AssetPriority priority)
    {
        var clumpHandle = registry.LoadBackdropClump(modelName, priority);
        return LoadClumpMaterialFor(registry, entity, clumpHandle, variant, placeholder, priority);
    }

    private static AssetHandle<ClumpAsset> LoadClumpMaterialFor(IAssetRegistry registry, DefaultEcs.Entity entity,
        AssetHandle<ClumpAsset> clumpHandle,
        ModelMaterial.Variant variant,
        StandardTextureKind placeholder,
        AssetPriority priority)
    {
        registry.Apply(clumpHandle, LoadMaterials);
        entity.Set(clumpHandle.AsDuplicate());
        return clumpHandle;

        void LoadMaterials(AssetHandle<ClumpAsset> clumpHandle)
        {
            if (!entity.IsAlive)
                return;
            var mesh = clumpHandle.Get().Mesh;
            entity.Set(mesh);

            var materials = new List<ModelMaterial>(mesh.Materials.Count);
            var materialHandles = new AssetHandle[mesh.Materials.Count];
            for (int i = 0; i < mesh.Materials.Count; i++)
            {
                var materialHandle = registry.LoadClumpMaterial(
                    mesh.Materials[i],
                    variant,
                    placeholder,
                    AssetPriority.Synchronous);
                materials.Add(materialHandle.Get().Material);
                materialHandles[i] = materialHandle.As();
            }
            entity.Set(materials);
            entity.Set(materialHandles);
        }
    }
}
