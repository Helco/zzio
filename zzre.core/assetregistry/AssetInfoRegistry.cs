using System;
using System.Collections.Generic;

namespace zzre;

public enum AssetLocality
{
    Global,
    Context,
    SingleUsage,
}

public static class AssetInfoRegistry<TInfo> where TInfo : IEquatable<TInfo>
{
    public delegate Asset AssetConstructor(IAssetRegistry registry, Guid assetId, in TInfo info);

    private static readonly object @lock = new();
    private static readonly Dictionary<TInfo, Guid> infoToGuid = [];
    private static readonly Dictionary<Guid, TInfo> guidToInfo = [];
    private static AssetConstructor? constructor;
    internal static string Name { get; private set; } = typeof(TInfo).FullName ?? "Unknown";
    internal static AssetLocality Locality { get; private set; }

    public static void Register(string name, AssetConstructor constructor, AssetLocality locality)
    {
        if (AssetInfoRegistry<TInfo>.constructor != null)
            throw new InvalidOperationException($"Asset type with info {typeof(TInfo).FullName} was already registered");
        AssetInfoRegistry<TInfo>.Locality = locality;
        AssetInfoRegistry<TInfo>.Name = name;
        AssetInfoRegistry<TInfo>.constructor = constructor;
    }

    public static void Register<TAsset>(AssetLocality locality) where TAsset : Asset
    {
        var ctorInfo =
            typeof(TAsset).GetConstructor([typeof(IAssetRegistry), typeof(Guid), typeof(TInfo)])
            ?? throw new ArgumentException("Could not find standard constructor", nameof(TAsset));
        Register(typeof(TAsset).Name, (IAssetRegistry registry, Guid guid, in TInfo info) =>
            (TAsset)ctorInfo.Invoke([registry, guid, info]), locality);
    }

    internal static Asset Construct(IAssetRegistry registry, Guid assetId, in TInfo info)
    {
        EnsureRegistered();
        return constructor!(registry, assetId, info);
    }

    private static void EnsureRegistered()
    {
        if (constructor == null)
            throw new InvalidOperationException($"Asset type with info {typeof(TInfo).FullName} was used before being registered");
    }

    public static Guid ToGuid(in TInfo info)
    {
        EnsureRegistered();
        if (Locality is AssetLocality.SingleUsage)
            // as single usage we should not save the GUID as to not leak memory
            return Guid.NewGuid();
        lock (@lock)
        {
            if (infoToGuid.TryGetValue(info, out var guid))
                return guid;
            do
            {
                guid = Guid.NewGuid();
            } while (guidToInfo.ContainsKey(guid));
            infoToGuid.Add(info, guid);
            guidToInfo.Add(guid, info);
            return guid;
        }
    }

    public static TInfo ToInfo(Guid assetId)
    {
        EnsureRegistered();
        lock(@lock)
        {
            if (guidToInfo.TryGetValue(assetId, out var info))
                return info;
            throw new KeyNotFoundException($"Could not find registered info for {assetId}");
        }
    }
}
