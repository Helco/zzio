using DefaultEcs.Resource;
using zzio;
using zzio.effect;
using zzio.vfs;

namespace zzre.game.resources;

public class EffectCombiner : AResourceManager<int, zzio.effect.EffectCombiner>
{
    private static readonly FilePath BasePath = new("resources/effects/");
    private const string FileExtension = ".ed";
    private readonly IResourcePool resourcePool;

    public EffectCombiner(ITagContainer diContainer)
    {
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override zzio.effect.EffectCombiner Load(int info)
    {
        var path = BasePath.Combine($"e{info}{FileExtension}");
        using var stream = resourcePool.FindAndOpen(path) ??
            throw new System.IO.FileNotFoundException($"Could not find effect combiner: {path}");
        var eff = new zzio.effect.EffectCombiner();
        eff.Read(stream);
        return eff;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, int info, zzio.effect.EffectCombiner resource)
    {
        entity.Set(resource);
    }
}
