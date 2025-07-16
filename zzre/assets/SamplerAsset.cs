using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Veldrid;

namespace zzre;

public sealed class SamplerAsset(IAssetRegistry registry) : IAsset<SamplerDescription>
{
    public IAssetRegistry Registry { get; } = registry;
    public string DebugName { get; private init; } = "";
    public Sampler Sampler { get; private set; } = null!;

    static Task<AssetLoadResult<SamplerDescription>> IAsset<SamplerDescription>.LoadAsync(IAssetRegistry registry, SamplerDescription info, CancellationToken ct)
    {
        var resourceFactory = registry.DIContainer.GetTag<ResourceFactory>();
        var sampler = resourceFactory.CreateSampler(info);
        var debugName = GetDebugName(info);
        sampler.Name = debugName;
        return Task.FromResult(new AssetLoadResult<SamplerDescription>(
            new SamplerAsset(registry)
            {
                DebugName = debugName,
                Sampler = sampler
            }
        ));
    }

    private static string GetDebugName(in SamplerDescription info)
    {
        var stringBuilder = new StringBuilder("Sampler ");
        stringBuilder.Append(info.Filter);
        stringBuilder.Append(' ');
        stringBuilder.Append(info.AddressModeU);
        if (info.AddressModeV != info.AddressModeU)
        {
            stringBuilder.Append(',');
            stringBuilder.Append(info.AddressModeV);
        }
        if (info.MaximumLod == 0)
            stringBuilder.Append(" (No LOD)");
        return stringBuilder.ToString();
    }

    public void Dispose()
    {
        Sampler?.Dispose();
        Sampler = null!;
    }

    public override string ToString() => DebugName;
}

static partial class AssetExtensions
{
    public static AssetHandle<SamplerAsset> LoadSampler(this IAssetRegistry registry,
        in SamplerDescription info,
        AssetPriority priority = AssetPriority.Synchronous) =>
        registry.Load<SamplerDescription, SamplerAsset>(info, priority);
}
