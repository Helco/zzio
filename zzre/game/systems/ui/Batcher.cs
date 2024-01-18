using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzre.materials;
using zzio;

namespace zzre.game.systems.ui;

public partial class Batcher : AEntitySortedSetSystem<CommandList, components.ui.RenderOrder>
{
    public record struct Batch(UIMaterial Material, uint Instances);

    private readonly List<Batch> batches = new();
    private readonly UI ui;
    private readonly GraphicsDevice graphicsDevice;
    private readonly ResourceFactory resourceFactory;
    private readonly UIMaterial untexturedMaterial;
    private readonly Texture emptyTexture;

    private UIMaterial? lastMaterial;
    private UIInstanceBuffer instanceBuffer;
    private uint nextInstanceCount;

    public Batcher(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        ui = diContainer.GetTag<UI>();
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = diContainer.GetTag<ResourceFactory>();
        instanceBuffer = new(diContainer);

        emptyTexture = resourceFactory.CreateTexture(
            new TextureDescription(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
        untexturedMaterial = new UIMaterial(diContainer);
        untexturedMaterial.Texture.Texture = emptyTexture;
        untexturedMaterial.ScreenSize.Buffer = diContainer.GetTag<UI>().ProjectionBuffer;
    }

    public override void Dispose()
    {
        base.Dispose();
        instanceBuffer.Dispose();
        untexturedMaterial.Dispose();
        emptyTexture.Dispose();
    }

    [WithPredicate]
    private bool IsVisible(in components.Visibility visibility) => visibility == components.Visibility.Visible;

    protected override void PreUpdate(CommandList _)
    {
        lastMaterial = null;
        nextInstanceCount = 0;
        batches.Clear();

        var tiles = World.GetComponents<components.ui.Tile[]>();
        var totalRects = 0;
        foreach (var entity in SortedSet.GetEntities())
            totalRects += tiles[entity].Length;
        instanceBuffer.Clear();
        instanceBuffer.Reserve(totalRects, additive: false);
    }

    [Update]
    private void Update(
        CommandList cl,
        in DefaultEcs.Entity entity,
        in components.ui.UIOffset offset,
        UIMaterial? material,
        IColor color,
        components.ui.Tile[] tiles)
    {
        var newMaterial = material ?? lastMaterial ?? untexturedMaterial;
        if (lastMaterial != newMaterial)
            FinishBatch();
        lastMaterial = newMaterial;

        foreach (var tile in tiles)
        {
            var uvRectangle = Rect.FromMinMax(Vector2.Zero, Vector2.One);
            if (tile.TileId >= 0)
            {
                var tileSheet = entity.Get<rendering.TileSheet>();
                uvRectangle = tileSheet[tile.TileId];
            }

            instanceBuffer.Add(new()
            {
                pos = offset.Calc(tile.Rect.Min, ui.LogicalScreen),
                size = tile.Rect.Size,
                color = color,
                textureWeight = material == null ? 0f : 1f,
                uvPos = uvRectangle.Min,
                uvSize = uvRectangle.Size
            });
            nextInstanceCount++;
        }
    }

    protected override void PostUpdate(CommandList cl)
    {
        FinishBatch();
        if (batches.Count == 0)
            return;

        cl.PushDebugGroup("UIBatcher");
        // TODO: Update UI batches only if changed
        instanceBuffer.Update(cl);
        untexturedMaterial.ApplyAttributes(cl, instanceBuffer);
        uint instanceStart = 0;
        foreach (var batch in batches)
        {
            (batch.Material as rendering.IMaterial).Apply(cl);
            cl.Draw(
                vertexStart: 0,
                vertexCount: 4,
                instanceStart: instanceStart,
                instanceCount: batch.Instances);
            instanceStart += batch.Instances;
        }
        cl.PopDebugGroup();
    }

    private void FinishBatch()
    {
        if (lastMaterial == null || nextInstanceCount == 0)
            return;
        batches.Add(new(lastMaterial, nextInstanceCount));
        lastMaterial = null;
        nextInstanceCount = 0;
    }
}
