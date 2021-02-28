using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzre;

namespace zzmaps
{
    class MapTiler
    {
        private readonly int globalMinZoomLevel = int.MinValue;
        private readonly int globalMaxZoomLevel = int.MaxValue;
        private readonly bool ignorePPU = false;

        public float ExtraBorder { get; set; } = 0f;
        public float BasePixelsPerUnit { get; set; } = 1f;
        public float MinPixelsPerUnit { get; set; } = 32f;

        public int TilePixelSize { get; set; } = 1024;
        public Box WorldUnitBounds { get; set; }

        public float ZoomFactorAt(int zoomLevel) => MathF.Pow(2f, zoomLevel);
        public float TileUnitSizeAt(int zoomLevel) => TilePixelSize / BasePixelsPerUnit / ZoomFactorAt(zoomLevel);

        public MapTiler(ITagContainer? diContainer = null)
        {
            if (diContainer == null || !diContainer.TryGetTag<Options>(out var options))
                return;

            if (options.MinZoom.HasValue)
                globalMinZoomLevel = options.MinZoom.Value;
            if (options.MaxZoom.HasValue)
                globalMaxZoomLevel = options.MaxZoom.Value;
            ignorePPU = options.IgnorePPU;
            if (ignorePPU && (!options.MinZoom.HasValue || !options.MaxZoom.HasValue))
                throw new InvalidOperationException("Cannot ignore PPU without explicitly set minimum and maximum zoom levels");

            ExtraBorder = options.ExtraBorder;
            BasePixelsPerUnit = options.BasePPU;
            MinPixelsPerUnit = options.MinPPU;
            TilePixelSize = options.TileSize;
        }

        public Box TileUnitBoundsFor(int tileX, int tileZ, int zoomLevel)
        {
            float tileUnitSize = TileUnitSizeAt(zoomLevel);
            return new Box(
                center: new Vector3(
                    WorldUnitBounds.Min.X + (tileX + 0.5f) * tileUnitSize,
                    WorldUnitBounds.Center.Y,
                    WorldUnitBounds.Min.Z + (tileZ + 0.5f) * tileUnitSize),
                size: new Vector3(tileUnitSize, WorldUnitBounds.Size.Y, tileUnitSize));
        }

        private (int minTile, int maxTile) TileRangeForAndAt(float min, float max, int zoomLevel) => (
            (int)Math.Floor((min - ExtraBorder) / TileUnitSizeAt(zoomLevel)),
            (int)Math.Ceiling((max + ExtraBorder) / TileUnitSizeAt(zoomLevel)));

        private int TileCountForAndAt(float min, float max, int zoomLevel) =>
            TileRangeForAndAt(min, max, zoomLevel).maxTile - TileRangeForAndAt(min, max, zoomLevel).minTile + 1;

        public (int minTile, int maxTile) TileRangeForXAt(int zoomLevel) =>
            TileRangeForAndAt(WorldUnitBounds.Min.X, WorldUnitBounds.Max.X, zoomLevel);
        public int TileCountForXAt(int zoomLevel) =>
            TileCountForAndAt(WorldUnitBounds.Min.X, WorldUnitBounds.Max.X, zoomLevel);

        public (int minTile, int maxTile) TileRangeForZAt(int zoomLevel) =>
            TileRangeForAndAt(WorldUnitBounds.Min.Z, WorldUnitBounds.Max.Z, zoomLevel);
        public int TileCountForZAt(int zoomLevel) =>
            TileCountForAndAt(WorldUnitBounds.Min.Z, WorldUnitBounds.Max.Z, zoomLevel);

        public int MinZoomLevelFor(float min, float max)
        {
            // TODO: Get the formula for that
            for (int i = 1; ;i++)
            {
                if (TileCountForAndAt(min, max, i) > 1)
                    return i - 1;
            }
        }

        public int MinZoomLevel => // the zoom level where the next one spans 2 tiles
            ignorePPU ? globalMinZoomLevel :
            Math.Max(globalMinZoomLevel, Math.Min(
                MinZoomLevelFor(WorldUnitBounds.Min.X, WorldUnitBounds.Max.X),
                MinZoomLevelFor(WorldUnitBounds.Min.Z, WorldUnitBounds.Max.Z)));

        public int MaxZoomLevel => // the reverse of BasePixelsPerUnit * 2^MaxZoomLevel >= MinPixelsPerUnit
            ignorePPU ? globalMaxZoomLevel :
            Math.Min(globalMaxZoomLevel,
            (int)Math.Ceiling(Math.Log2(MinPixelsPerUnit / BasePixelsPerUnit)) + 1);

        public int ZoomLevelCount => MaxZoomLevel - MinZoomLevel + 1;

        public IEnumerable<(int tileX, int tileZ)> TilesAt(int zoomLevel) => Enumerable
            .Range(TileRangeForXAt(zoomLevel).minTile, TileCountForXAt(zoomLevel))
            .SelectMany(x => Enumerable
                .Range(TileRangeForZAt(zoomLevel).minTile, TileCountForZAt(zoomLevel))
                .Select(z => (x, z)));

        public IEnumerable<(int zoomLevel, int tileX, int tileZ)> Tiles => Enumerable
            .Range(MinZoomLevel, ZoomLevelCount)
            .SelectMany(zoomLevel => TilesAt(zoomLevel)
                .Select(t => (zoomLevel, t.tileX, t.tileZ)));

        public IEnumerable<Box> TileUnitBounds => Tiles.Select(t => TileUnitBoundsFor(t.tileX, t.tileZ, t.zoomLevel));

        public int TileCountAt(int zoomLevel) =>
            TileCountForXAt(zoomLevel) * TileCountForZAt(zoomLevel);

        public int TileCount => Enumerable
            .Range(MinZoomLevel, ZoomLevelCount)
            .Sum(TileCountAt);
    }
}
