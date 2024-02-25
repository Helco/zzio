using System.Text;
using System.Threading;

namespace zzre;

public struct AssetRegistryStats
{
    private int created;
    private int loaded;
    private int removed;

    public int Created => created;
    public int Loaded => loaded;
    public int Removed => removed;

    internal void OnAssetCreated() => Interlocked.Increment(ref created);
    internal void OnAssetLoaded() => Interlocked.Increment(ref loaded);
    internal void OnAssetRemoved() => Interlocked.Increment(ref removed);

    public static AssetRegistryStats operator -(AssetRegistryStats lhs, AssetRegistryStats rhs) => new()
    {
        created = lhs.created - rhs.created,
        loaded = lhs.loaded - rhs.loaded,
        removed = lhs.removed - rhs.removed
    };

    public static AssetRegistryStats operator +(AssetRegistryStats lhs, AssetRegistryStats rhs) => new()
    {
        created = rhs.created + lhs.created,
        loaded = rhs.loaded + lhs.loaded,
        removed = rhs.removed + lhs.removed
    };

    public override string ToString()
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
