using DefaultEcs.Resource;
using zzio;
using zzio.effect;
using zzio.vfs;

namespace zzre.game.resources;

public class EffectCombiner : AResourceManager<string, zzio.effect.EffectCombiner>
{
    private static readonly FilePath BasePath = new("resources/effects/");
    private const string FileExtension = ".ed";
    private readonly IResourcePool resourcePool;

    public EffectCombiner(ITagContainer diContainer)
    {
        resourcePool = diContainer.GetTag<IResourcePool>();
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override zzio.effect.EffectCombiner Load(string info)
    {
        var path = BasePath.Combine(info + FileExtension);
        using var stream = resourcePool.FindAndOpen(path) ??
            throw new System.IO.FileNotFoundException($"Could not find effect combiner: {path}");
        var eff = new zzio.effect.EffectCombiner();
        eff.Read(stream);
        return eff;
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, string info, zzio.effect.EffectCombiner resource)
    {
        entity.Set(resource);
    }
}
