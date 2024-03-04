using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace zzre;

/// <summary>The locality of an asset type</summary>
/// <remarks>This will determine whether an asset is loaded at a global or a local registry</remarks>
public enum AssetLocality
{
    /// <summary>Global assets will be loaded at a global registry and shared across all users</summary>
    Global,
    /// <summary>Context assets will be loaded at a local registry and shared across users of the local registry</summary>
    Context,
    /// <summary>SingleUsage assets will be loaded at a local registry and never shared</summary>
    SingleUsage,
}

/// <summary>The application-wide registry of asset types and mapping of info values to asset IDs</summary>
/// <typeparam name="TInfo">The type of info values denoting a specific asset type</typeparam>
public static class AssetInfoRegistry<TInfo> where TInfo : IEquatable<TInfo>
{
    /// <summary>Constructs an asset instance</summary>
    /// <param name="registry">The registry the asset is registered at</param>
    /// <param name="assetId">The ID of the asset</param>
    /// <param name="info">The info value given to the <c>Load</c> method</param>
    /// <returns>A new instance of this asset type</returns>
    public delegate Asset AssetConstructor(IAssetRegistry registry, Guid assetId, in TInfo info);

    private static readonly object @lock = new();
    private static readonly Dictionary<TInfo, Guid> infoToGuid = [];
    private static readonly Dictionary<Guid, TInfo> guidToInfo = [];
    private static AssetConstructor? constructor;
    internal static string Name { get; private set; } = typeof(TInfo).FullName ?? "Unknown";
    internal static AssetLocality Locality { get; private set; }

    /// <summary>Registers this asset type with a manual constructor delegate</summary>
    /// <remarks>Asset types can only be registered once and have to be registered before attempting to load assets</remarks>
    /// <param name="name">The debug name of this asset type</param>
    /// <param name="constructor">The delegate constructing asset instances</param>
    /// <param name="locality">The locality of this asset type</param>
    public static void Register(string name, AssetConstructor constructor, AssetLocality locality)
    {
        if (AssetInfoRegistry<TInfo>.constructor != null)
            throw new InvalidOperationException($"Asset type with info {typeof(TInfo).FullName} was already registered");
        AssetInfoRegistry<TInfo>.Locality = locality;
        AssetInfoRegistry<TInfo>.Name = name;
        AssetInfoRegistry<TInfo>.constructor = constructor;
    }

    /// <summary>Registers this asset type with a reflected class inheriting from <see cref="Asset"/></summary>
    /// <remarks>Asset types can only be registered once and have to be registered before attempting to load assets</remarks>
    /// <typeparam name="TAsset">A class inheriting from <see cref="Asset"/> with a public constructor (<see cref="IAssetRegistry"/>, <see cref="Guid"/>, <typeparamref name="TInfo"/>)</typeparam>
    /// <param name="locality">The locality of this asset type</param>
    public static void Register
        <[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAsset>
        (AssetLocality locality) where TAsset : Asset
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

    internal static Guid ToGuid(in TInfo info)
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

    internal static TInfo ToInfo(Guid assetId)
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
