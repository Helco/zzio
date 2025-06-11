using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public enum AssetLocality
{
    Global,
    Local,
    Unique
}

public interface IAsset : IDisposable
{
    static abstract AssetLocality Locality { get; }
    static abstract Type InfoType { get; }
    IAssetRegistry Registry { get; }

    private static readonly object generalInfoLock = new();
    private static readonly Dictionary<object, Guid> generalInfoToGuid = [];
    internal static Guid GeneralInfoToGuid(object info)
    {
        lock (generalInfoLock)
        {
            if (generalInfoToGuid.TryGetValue(info, out var guid))
                return guid;
            generalInfoToGuid.Add(info, guid = Guid.NewGuid());
            return guid;
        }
    }
}

public readonly record struct AssetLoadResult<TInfo>(
    IAsset<TInfo> Asset,
    IReadOnlyList<IAssetHandle> SecondaryAssets
) where TInfo : struct, IEquatable<TInfo>;

public interface IAsset<TInfo> : IAsset
    where TInfo : struct, IEquatable<TInfo>
{
    static Type IAsset.InfoType => typeof(TInfo);

    static virtual Guid InfoToAssetId(in TInfo info) => GeneralInfoToGuid(info);
    static abstract Task<AssetLoadResult<TInfo>> LoadAsync(in TInfo info, CancellationToken ct);
}

