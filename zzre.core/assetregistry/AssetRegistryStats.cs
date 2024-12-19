using System.Text;
using System.Threading;

namespace zzre;

/// <summary>
/// A snapshot of mostly monotonous counters of an <see cref="IAssetRegistry"/>
/// </summary>
public struct AssetRegistryStats
{
    private int created;
    private int loaded;
    private int removed;
    private int total;

    /// <summary>The number of assets created</summary>
    public readonly int Created => created;
    /// <summary>The number of assets that finished loading</summary>
    public readonly int Loaded => loaded;
    /// <summary>The number of assets removed from the registry</summary>
    public readonly int Removed => removed;
    /// <summary>The number of currently registered assets</summary>
    /// <remarks>This counter is not monotonous</remarks>
    public readonly int Total => total;

    internal void OnAssetCreated()
    {
        Interlocked.Increment(ref created);
        Interlocked.Increment(ref total);
    }

    internal void OnAssetLoaded() => Interlocked.Increment(ref loaded);

    internal void OnAssetRemoved()
    {
        Interlocked.Increment(ref removed);
        Interlocked.Decrement(ref total);
    }

    public static AssetRegistryStats operator -(AssetRegistryStats lhs, AssetRegistryStats rhs) => new()
    {
        created = lhs.created - rhs.created,
        loaded = lhs.loaded - rhs.loaded,
        removed = lhs.removed - rhs.removed,
        total = lhs.total - rhs.total,
    };

    public static AssetRegistryStats operator +(AssetRegistryStats lhs, AssetRegistryStats rhs) => new()
    {
        created = rhs.created + lhs.created,
        loaded = rhs.loaded + lhs.loaded,
        removed = rhs.removed + lhs.removed,
        total = rhs.total + lhs.total,
    };

    public override readonly string ToString()
    {
        var builder = new StringBuilder(256);
        builder.Append("Created: ");
        builder.Append(created);
        builder.Append("   Loaded: ");
        builder.Append(loaded);
        builder.Append("   Removed: ");
        builder.Append(removed);
        return builder.ToString();
    }
}
