using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace zzmaps
{
    internal enum ZZMapsImageFormat
    {
        Png,
        Jpg
    }

    internal enum ZZMapsGraphicsBackend
    {
        Vulkan,
        D3D11
    }

    internal static class ZZMaps
    {
        static Task Main(string[] args)
        {
            var defaultTiler = new MapTiler();
            var rootCommand = new RootCommand()
            {
                // Input and output
                new Option<DirectoryInfo[]>(new[] { "--resource-path" }, "Resource paths to use")
                .ExistingOnly(),
                new Option<FileInfo[]>(new[] { "--pak" }, "PAK archive to use")
                .ExistingOnly(),
                new Option<DirectoryInfo>(new[] { "--output-dir" }, "Output to files at that directory")
                .LegalFilePathsOnly(),
                new Option<FileInfo>(new[] { "--output-db" }, "Output to a SQLite database at that directory")
                .LegalFilePathsOnly(),
                new Option<bool>(new[] { "--replace-existing" }, () => false, "Replace existing rendered files/blobs"),
                new Option<Regex>(new[] { "--scene-pattern" }, () => new Regex("^sc_"), "RegEx pattern selecting scenes to render"),

                // Tiler
                new Option<float>(new[] { "--extra-border" }, () => defaultTiler.ExtraBorder, "Extra border around rendered maps"),
                new Option<float>(new[] { "--base-ppu" }, () => defaultTiler.BasePixelsPerUnit, "Base pixels-per-unit value (for zoom-level 0)"),
                new Option<float>(new[] { "--min-ppu" }, () => defaultTiler.MinPixelsPerUnit, "Minimum pixels-per-unit value (before rendering further zoom-levels)"),
                new Option<int>(new[] { "--tile-size" }, () => defaultTiler.TilePixelSize, "Size of one tile in pixels"),
                new Option<int?>(new[] { "--min-zoom" }, "Minimum zoom level"),
                new Option<int?>(new[] { "--max-zoom" }, "Maximum zoom level"),
                new Option<bool>(new[] { "--ignore-ppu" }, () => false, "Ignore Pixels-per-Unit for determining zoom levels to render"),

                // Encoder
                new Option<ZZMapsImageFormat>(new[] { "--output-format" }, () => ZZMapsImageFormat.Jpg, "Output image format"),
                new Option<ZZMapsImageFormat>(new[] { "--temp-format" }, () => ZZMapsImageFormat.Png, "Format given to optimizer"),
                new Option<string?>(new[] { "--optimizer" }, "Optimizer to process images")
                .AddSuggestions("brotli.exe --quality 80 $input $output"),

                // Scheduler
                new Option<ZZMapsGraphicsBackend>("--backend", () => ZZMapsGraphicsBackend.Vulkan, "The graphics backend to use"),
                new Option<int>("--renderers", () => Environment.ProcessorCount, "The maximum number of parallel render jobs"),
                new Option<int>("--cache-clean", () => Environment.ProcessorCount * 2, "The number of scenes rendered before cleaning the asset caches"),
                new Option<int>("--queue-load", () => 1, "The number of scenes to preload for rendering per job"),
                new Option<int>("--queue-render", () => 0, "The number of tiles to prerender for encoding per job"),
                new Option<int>("--queue-output", () => 0, "The number of tiles to preencode for outputting per job")
            };

            return rootCommand.InvokeAsync(args);
        }
    }

    internal class Options
    {
        public DirectoryInfo[] ResourcePath { get; set; } = Array.Empty<DirectoryInfo>();
        public FileInfo[] PAK { get; set; } = Array.Empty<FileInfo>();
        public DirectoryInfo? OutputDir { get; set; }
        public FileInfo? OutputDb { get; set; }
        public bool ReplaceExisting { get; set; }
        public Regex? ScenePatter { get; set; }
        
        public float ExtraBorder { get; set; }
        public float BasePPU { get; set; }
        public float MinPPU { get; set; }
        public int TileSize { get; set; }
        public int? MinZoom { get; set; }
        public int? MaxZoom { get; set; }
        public bool IgnorePPU { get; set; }

        public ZZMapsImageFormat OutputFormat { get; set; }
        public ZZMapsImageFormat TempFormat { get; set; }
        public string? Optimizer { get; set; }

        public ZZMapsGraphicsBackend Backend { get; set; }
        public int Renderers { get; set; }
        public int CacheClean { get; set; }
        public int QueueLoad { get; set; }
        public int QueueRender { get; set; }
        public int QueueOutput { get; set; }
    }
}
