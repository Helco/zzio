using System;
using System.Collections.Generic;

namespace zzre;

internal static class AssetInfoRegistry<TInfo> where TInfo : IEquatable<TInfo>
{
    private static readonly object @lock = new();
    private static readonly Dictionary<TInfo, Guid> infoToGuid = [];
    private static readonly Dictionary<Guid, TInfo> guidToInfo = [];

    public static Guid ToGuid(in TInfo info)
    {
        lock(@lock)
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

    public static TInfo ToInfo(Guid guid)
    {
        lock(@lock)
        {
            if (guidToInfo.TryGetValue(guid, out var info))
                return info;
            throw new KeyNotFoundException($"Could not find registered info for {guid}");
        }
    }
}
