using System;
using System.Collections.Generic;
using zzio;
using zzio.vfs;

namespace zzre;

public sealed class ActorAsset : Asset
{
    private static readonly FilePath BasePath = new("resources/models/actorsex");
    private const AssetLoadPriority SecondaryPriority = AssetLoadPriority.High;

    public readonly record struct Info(string Name)
    {
        public FilePath FullPath => BasePath.Combine(Name + ".aed");
    }

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<ActorAsset>(AssetLocality.Global);

    private readonly Info info;
    private ActorExDescription? description;
    private AssetHandle<AnimationAsset>[] bodyAnimations = [];
    private AssetHandle<AnimationAsset>[] wingsAnimations = [];

    public string Name => info.Name;
    public ActorExDescription Description => description ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public AssetHandle<ClumpAsset> Body { get; set; } = AssetHandle<ClumpAsset>.Invalid;
    public AssetHandle<ClumpAsset> Wings { get; set; } = AssetHandle<ClumpAsset>.Invalid;
    public IReadOnlyList<AssetHandle<AnimationAsset>> BodyAnimations => bodyAnimations;
    public IReadOnlyList<AssetHandle<AnimationAsset>> WingsAnimations => wingsAnimations;

    public ActorAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
    }

    protected override IEnumerable<AssetHandle> Load()
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new System.IO.FileNotFoundException($"Could not find actor: {info.Name}");
        description = ActorExDescription.ReadNew(stream);
        (Body, bodyAnimations) = LoadSecondaryPart(description.body);
        if (description.HasWings)
            (Wings, wingsAnimations) = LoadSecondaryPart(description.wings);

        var secondaryHandles = new AssetHandle[(description.HasWings ? 2 : 1) + bodyAnimations.Length + wingsAnimations.Length];
        var outI = 0;
        AddSecondaryHandles(secondaryHandles, ref outI, Body, bodyAnimations);
        if (description.HasWings)
            AddSecondaryHandles(secondaryHandles, ref outI, Wings, wingsAnimations);
        return secondaryHandles;
    }

    private (AssetHandle<ClumpAsset>, AssetHandle<AnimationAsset>[]) LoadSecondaryPart(ActorPartDescription part)
    {
        var clump = Registry.Load(ClumpAsset.Info.Actor(part.model), SecondaryPriority).As<ClumpAsset>();
        var animations = new AssetHandle<AnimationAsset>[part.animations.Length];
        for (int i = 0; i < animations.Length; i++)
            animations[i] = Registry.Load(new AnimationAsset.Info(part.animations[i].filename), SecondaryPriority).As<AnimationAsset>();
        return (clump, animations);
    }

    private static void AddSecondaryHandles(AssetHandle[] handles, ref int outI, AssetHandle clump, AssetHandle<AnimationAsset>[] animations)
    {
        handles[outI++] = clump;
        for (int i = 0; i < animations.Length; i++)
            handles[outI++] = animations[i];
    }

    protected override void Unload()
    {
        description = null;
        bodyAnimations = [];
        wingsAnimations = [];
    }

    protected override string ToStringInner() => $"Actor {info.Name}";
}
