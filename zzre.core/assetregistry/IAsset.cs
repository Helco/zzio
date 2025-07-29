using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    [ExcludeFromCodeCoverage] // for some reason not called
    public static virtual AssetLocality Locality => AssetLocality.Global;
    public static virtual bool NeedsMainThreadDisposal => false;
    IAssetRegistry Registry { get; }
}

public readonly record struct AssetLoadResult<TInfo>(
    IAsset<TInfo> Asset
) where TInfo : struct, IEquatable<TInfo>;

public interface IAsset<TInfo> : IAsset
    where TInfo : struct, IEquatable<TInfo>
{
    static virtual Guid InfoToAssetId(in TInfo info) => GeneralInfoToGuid(info);
    static abstract Task<AssetLoadResult<TInfo>> LoadAsync(IAssetRegistry registry, Guid assetId, TInfo info, CancellationToken ct);

    private static readonly object generalInfoLock = new();
    private static readonly Dictionary<TInfo, Guid> generalInfoToGuid = [];
    internal static Guid GeneralInfoToGuid(in TInfo info)
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

