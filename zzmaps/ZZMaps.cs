using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.CommandLine.Invocation;
using zzre;
using zzre.rendering;
using Veldrid;
using zzio.vfs;
using System.Threading;
using zzio.db;

namespace zzmaps
{
    internal enum ZZMapsImageFormat
    {
        Png,
        Jpeg
    }

    internal enum ZZMapsGraphicsBackend
    {
        Vulkan,
        D3D11
    }

    internal enum ZZMapsBackground
    {
        Clear,
        Black,
        White,
        Scene,
        Fog
    }

    internal static class ZZMaps
    {
        static Task Main(string[] args)
        {
            var defaultOptions = new Options();
            var defaultTiler = new MapTiler(Box.Zero);
            var pngDefaultCompression = (int)SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.DefaultCompression;

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
                new Option<Regex>(new[] { "--scene-pattern" }, () => defaultOptions.ScenePattern, "RegEx pattern selecting scenes to render"),
                new Option<uint>("--commit-every", () => defaultOptions.CommitEvery, "Commit the SQLite transaction every n tiles"),

                // Tiler
                new Option<float>(new[] { "--extra-border" }, () => defaultTiler.ExtraBorder, "Extra border around rendered maps"),
                new Option<float>(new[] { "--base-ppu" }, () => defaultTiler.BasePixelsPerUnit, "Base pixels-per-unit value (for zoom-level 0)"),
                new Option<float>(new[] { "--min-ppu" }, () => defaultTiler.MinPixelsPerUnit, "Minimum pixels-per-unit value (before rendering further zoom-levels)"),
                new Option<uint>(new[] { "--tile-size" }, () => defaultTiler.TilePixelSize, "Size of one tile in pixels"),
                new Option<int?>(new[] { "--min-zoom" }, "Minimum zoom level"),
                new Option<int?>(new[] { "--max-zoom" }, "Maximum zoom level"),
                new Option<bool>(new[] { "--ignore-ppu" }, () => false, "Ignore Pixels-per-Unit for determining zoom levels to render"),

                // Encoder
                new Option<ZZMapsBackground>( "--background", () => ZZMapsBackground.Scene, "The background color of the output images"),
                new Option<ZZMapsImageFormat>(new[] { "--output-format" }, () => ZZMapsImageFormat.Jpeg, "Output image format"),
                new Option<ZZMapsImageFormat>(new[] { "--temp-format" }, () => ZZMapsImageFormat.Png, "Format given to optimizer"),
                new Option<int>(new[] { "--jpeg-quality" }, () => 75, "JPEG quality level"),
                new Option<int>(new[] { "--png-compression" }, () => pngDefaultCompression, "PNG compression level"),
                new Option<string?>(new[] { "--optimizer" }, "Optimizer to process images")
                .AddSuggestions("brotli.exe --quality 80 $input $output"),
                new Option<DirectoryInfo>("--temp-folder", () => defaultOptions.TempFolder, "Temporary folder for optimization")
                .LegalFilePathsOnly(),

                // Scheduler
                new Option<ZZMapsGraphicsBackend>("--backend", () => ZZMapsGraphicsBackend.Vulkan, "The graphics backend to use"),
                new Option<uint>("--renderers", () => (uint)Environment.ProcessorCount, "The maximum number of parallel render jobs"),
                new Option<uint>("--cache-clean", () => (uint)(Environment.ProcessorCount * 2), "The number of scenes rendered before cleaning the asset caches")
            };

            rootCommand.TreatUnmatchedTokensAsErrors = true;
            rootCommand.Handler = CommandHandler.Create<Options>(HandleCommand);
            return rootCommand.InvokeAsync(args);
        }

        private static void HandleCommand(Options options)
        {
            var diContainer = SetupDIContainer(options, out var graphicsDevice);
            var scheduler = new Scheduler(diContainer);

            var runTask = scheduler.Run();
            int printedLines = 0;
            while(!runTask.IsCompleted)
            {
                PrintProgress();
                Thread.Sleep(500);
            }
            PrintProgress();
            Console.WriteLine($"Rendering {runTask.Status}");
            if (runTask.Exception != null)
                throw runTask.Exception;

            void PrintProgress()
            {
                Console.CursorTop -= printedLines;
                string emptyLine = new string(' ', Console.BufferWidth - 1);
                printedLines = 0;
                int maxNameLen = scheduler.ProgressSteps.Max(s => s.Name.Length);
                foreach (var step in scheduler.ProgressSteps)
                {
                    if (step.Current <= 0)
                        continue;
                    Console.Write(emptyLine);
                    Console.CursorLeft = 0;
                    if (step.Total == null)
                        Console.WriteLine($"{step.Name.PadLeft(maxNameLen, ' ')}:\t{step.Current}");
                    else
                        Console.WriteLine($"{step.Name.PadLeft(maxNameLen, ' ')}:\t{step.Current} / {step.Total}");
                    printedLines++;
                }
            }

            // dispose graphics device last, otherwise Vulkan will crash
            scheduler.Dispose();
            diContainer.RemoveTag<GraphicsDevice>();
            diContainer.Dispose();
            graphicsDevice.Dispose();
        }

        private static TagContainer SetupDIContainer(Options options, out GraphicsDevice graphicsDevice)
        {
            var diContainer = new TagContainer();
            diContainer.AddTag(options);

            var graphicsDeviceOptions = new GraphicsDeviceOptions()
            {
                Debug = true,
                HasMainSwapchain = false,
                PreferDepthRangeZeroToOne = true,
                PreferStandardClipSpaceYDirection = true,
            };
            graphicsDevice = options.Backend switch
            {
                ZZMapsGraphicsBackend.Vulkan => GraphicsDevice.CreateVulkan(graphicsDeviceOptions),
                ZZMapsGraphicsBackend.D3D11 => GraphicsDevice.CreateD3D11(graphicsDeviceOptions),
                _ => throw new InvalidOperationException($"Unknown backend {options.Backend}")
            };
            diContainer.AddTag(graphicsDevice);
            diContainer.AddTag(graphicsDevice.ResourceFactory);

            var pipelineCollection = new PipelineCollection(graphicsDevice);
            pipelineCollection.AddShaderResourceAssemblyOf<zzre.materials.ModelStandardMaterial>();
            pipelineCollection.AddShaderResourceAssemblyOf<MapStandardMaterial>();
            diContainer.AddTag(pipelineCollection);

            var resourcePools = options.ResourcePath
                .Select(dirInfo => new FileResourcePool(dirInfo.FullName) as IResourcePool);
            resourcePools = resourcePools.Concat(options.PAK
                .Select(fileInfo => new PAKParallelResourcePool(fileInfo.FullName)));
            var combinedResourcePool = new CombinedResourcePool(resourcePools.Reverse().ToArray());
            diContainer.AddTag<IResourcePool>(combinedResourcePool);

            diContainer.AddTag<IAssetLoader<Texture>>(new RefCachedAssetLoader<Texture>(
                new TextureAssetLoader(diContainer)));
            diContainer.AddTag<IAssetLoader<ClumpBuffers>>(new RefCachedAssetLoader<ClumpBuffers>(
                new ClumpAssetLoader(diContainer)));

            var mappedDb = new MappedDB();
            for (int i = 1; i <= 6; i++)
            {
                using var tableStream = combinedResourcePool.FindAndOpen($"Data/_fb0x0{i}.fbs");
                if (tableStream == null)
                    continue;
                var table = new Table();
                table.Read(tableStream);
                mappedDb.AddTable(table);
            }
            diContainer.AddTag(mappedDb);

            return diContainer;
        }

        internal static string AsExtension(this ZZMapsImageFormat format) => format switch
        {
            ZZMapsImageFormat.Jpeg => ".jpeg",
            ZZMapsImageFormat.Png => ".png",
            _ => throw new NotSupportedException($"Unsupported image formt {format}")
        };
    }

    internal class Options
    {
        public DirectoryInfo[] ResourcePath { get; set; } = Array.Empty<DirectoryInfo>();
        public FileInfo[] PAK { get; set; } = Array.Empty<FileInfo>();
        public DirectoryInfo? OutputDir { get; set; }
        public FileInfo? OutputDb { get; set; }
        public bool ReplaceExisting { get; set; }
        public Regex ScenePattern { get; set; } = new Regex("^sc_");
        public uint CommitEvery { get; set; } = 100;
        
        public float ExtraBorder { get; set; }
        public float BasePPU { get; set; }
        public float MinPPU { get; set; }
        public uint TileSize { get; set; }
        public int? MinZoom { get; set; }
        public int? MaxZoom { get; set; }
        public bool IgnorePPU { get; set; }

        public ZZMapsBackground Background { get; set; }
        public ZZMapsImageFormat OutputFormat { get; set; }
        public ZZMapsImageFormat TempFormat { get; set; }
        public int JPEGQuality { get; set; }
        public int PNGCompression { get; set; }
        public string? Optimizer { get; set; }
        public DirectoryInfo TempFolder { get; set; } = new DirectoryInfo("./temp");

        public ZZMapsGraphicsBackend Backend { get; set; }
        public uint Renderers { get; set; }
        public uint CacheClean { get; set; }
    }
}
