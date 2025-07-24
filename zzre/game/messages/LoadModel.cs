using zzio;
using zzio.scn;

namespace zzre.game.messages;

public readonly record struct LoadModel(
    DefaultEcs.Entity AsEntity,
    string ModelName,
    IColor Color,
    FOModelRenderType? RenderType = null,
    AssetPriority Priority = AssetPriority.Synchronous)
{
    public LoadModel(
        DefaultEcs.Entity AsEntity,
        string ModelName,
        FOModelRenderType? RenderType = null,
        AssetPriority Priority = AssetPriority.Synchronous)
        : this(AsEntity, ModelName, IColor.White, RenderType, Priority) { }
}