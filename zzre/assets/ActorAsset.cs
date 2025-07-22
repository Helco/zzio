using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using zzio;
using zzio.vfs;

namespace zzre;

public sealed class ActorAsset(IAssetRegistry registry, ActorAsset.Info info) : IAsset<ActorAsset.Info>
{
    private static readonly FilePath BasePath = new("resources/models/actorsex");

    public readonly record struct Info(string Name)
    {
        public FilePath FullPath => BasePath.Combine(Name + ".aed");
    }

    private readonly record struct Part(
        AssetHandle<ClumpAsset> ClumpHandle,
        AssetHandle<AnimationAsset>[] AnimHandles)
    {
        public readonly ClumpAsset? Clump = ClumpHandle.Asset;
        public readonly AnimationAsset[] Animations = [.. AnimHandles.Select(h => h.Asset ??
            throw new InvalidOperationException("Secondary asset was not loaded"))];

        public void Dispose()
        {
            ClumpHandle.Dispose();
            foreach (var animHandle in AnimHandles ?? [])
                animHandle.Dispose();
        }
    }

    private readonly Info info = info;
    private Part body, wings;

    public IAssetRegistry Registry { get; } = registry;
    public string Name => info.Name;

    public ActorExDescription Description { get; private init; } = null!;
    public ClumpAsset Body => body.Clump!;
    public ClumpAsset? Wings => wings.Clump;
    public ReadOnlySpan<AnimationAsset> BodyAnimations => body.Animations;
    public ReadOnlySpan<AnimationAsset> WingsAnimations => wings.Animations;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid _, Info info, CancellationToken ct)
    {
        var resourcePool = registry.DIContainer.GetTag<IResourcePool>();

        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find actor: {info.Name}");
        var description = ActorExDescription.ReadNew(stream);
        var body = LoadSecondaryPart(registry, description.body);
        var wings = new Part(default, []); 
        if (description.HasWings)
            wings = LoadSecondaryPart(registry, description.wings);

        var secondaryHandles = new Task[(description.HasWings ? 2 : 1) + body.Animations.Length + wings.Animations.Length];
        var outI = 0;
        AddSecondaryHandles(secondaryHandles, ref outI, body, ct);
        if (wings != default)
            AddSecondaryHandles(secondaryHandles, ref outI, wings, ct);
        await Task.WhenAll(secondaryHandles).WaitAsync(ct);

        return new AssetLoadResult<Info>(new ActorAsset(registry, info)
        {
            body = body,
            wings = wings,
            Description = description
        });
    }

    private static Part LoadSecondaryPart(IAssetRegistry registry, ActorPartDescription part)
    {
        var clump = registry.LoadActorClump(part.model, AssetPriority.High);
        var animations = new AssetHandle<AnimationAsset>[part.animations.Length];
        for (int i = 0; i < animations.Length; i++)
            animations[i] = registry.LoadAnimation(part.animations[i].filename, AssetPriority.High);
        return new(clump, animations);
    }

    private static void AddSecondaryHandles(Task[] handles, ref int outI, in Part part, CancellationToken ct)
    {
        handles[outI++] = part.ClumpHandle.GetAsync(ct).AsTask();
        foreach (var animHandle in part.AnimHandles)
            handles[outI++] = animHandle.GetAsync(ct).AsTask();
    }

    public void Dispose()
    {
        body.Dispose();
        wings.Dispose();
        body = wings = default;
    }

    public override string ToString() => $"Actor {info.Name}";
}

static partial class AssetExtensions
{
    public static AssetHandle<ActorAsset> LoadActor(this IAssetRegistry registry, string name, AssetPriority priority) =>
        registry.Load<ActorAsset.Info, ActorAsset>(new(name), priority);
}
