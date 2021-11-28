using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzre.materials;
using zzio;

namespace zzre.game.systems.ui
{
    public partial class Batcher : AEntitySortedSetSystem<CommandList, components.ui.RenderOrder>
    {
        public record struct Batch(UIMaterial Material, uint Instances);

        private readonly List<Batch> batches = new List<Batch>();
        private readonly GraphicsDevice graphicsDevice;
        private readonly ResourceFactory resourceFactory;
        private readonly UIMaterial untexturedMaterial;
        private readonly Texture emptyTexture;

        private UIMaterial? lastMaterial;
        private DeviceBuffer instanceBuffer;
        private MappedResourceView<UIInstance> mappedInstances;
        private int nextInstanceI;
        private uint nextInstanceCount;

        public Batcher(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            graphicsDevice = diContainer.GetTag<GraphicsDevice>();
            resourceFactory = diContainer.GetTag<ResourceFactory>();
            instanceBuffer = null!;

            emptyTexture = resourceFactory.CreateTexture(
                new TextureDescription(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
            untexturedMaterial = new UIMaterial(diContainer, isFont: false);
            untexturedMaterial.Texture.Texture = emptyTexture;
            untexturedMaterial.Projection.Buffer = diContainer.GetTag<UI>().ProjectionBuffer;
        }

        public override void Dispose()
        {
            base.Dispose();
            instanceBuffer?.Dispose();
            untexturedMaterial.Dispose();
            emptyTexture.Dispose();
        }

        [WithPredicate]
        private bool IsVisible(in components.Visibility visibility) => visibility == components.Visibility.Visible;

        protected override void PreUpdate(CommandList _)
        {
            lastMaterial = null;
            batches.Clear();
            nextInstanceI = 0;
            nextInstanceCount = 0;

            var tiles = World.GetComponents<components.ui.Tile[]>();
            var totalRects = 0;
            foreach (var entity in SortedSet.GetEntities())
                totalRects += tiles[entity].Length;

            uint totalSizeInBytes = UIInstance.Stride * (uint)totalRects;
            if ((instanceBuffer?.SizeInBytes ?? 0) < totalSizeInBytes)
            {
                instanceBuffer?.Dispose();
                instanceBuffer = resourceFactory.CreateBuffer(
                    new BufferDescription(totalSizeInBytes, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            }

            mappedInstances = graphicsDevice.Map<UIInstance>(instanceBuffer, MapMode.Write);
        }

        [Update]
        private void Update(
            CommandList cl,
            in DefaultEcs.Entity entity,
            UIMaterial? material,
            IColor color,
            components.ui.Tile[] tiles)
        {
            var newMaterial = material ?? lastMaterial ?? untexturedMaterial;
            if (lastMaterial != newMaterial)
                FinishBatch(cl);
            lastMaterial = newMaterial;

            foreach (var tile in tiles)
            {
                var uvRectangle = Rect.FromMinMax(Vector2.Zero, Vector2.One);
                if (tile.TileId >= 0)
                {
                    var tileSheet = entity.Get<rendering.TileSheet>();
                    uvRectangle = tileSheet[tile.TileId];
                }

                ref var instance = ref mappedInstances[nextInstanceI++];
                instance.center = tile.Rect.Center;
                instance.size = tile.Rect.HalfSize;
                instance.color = color;
                instance.textureWeight = material == null ? 0f : 1f;
                instance.uvCenter = uvRectangle.Center;
                instance.uvSize = uvRectangle.HalfSize;
                
                nextInstanceCount++;
            }
        }

        protected override void PostUpdate(CommandList cl)
        {
            graphicsDevice.Unmap(instanceBuffer);
            FinishBatch(cl);

            // TODO: Update UI batches only if changed
            cl.SetVertexBuffer(0, instanceBuffer);
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
        }

        private void FinishBatch(CommandList cl)
        {
            if (lastMaterial == null || nextInstanceCount == 0)
                return;
            batches.Add(new(lastMaterial, nextInstanceCount));
            lastMaterial = null;
            nextInstanceCount = 0;
        }
    }
}
