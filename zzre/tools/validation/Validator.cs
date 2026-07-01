using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Serilog;
using zzio.vfs;
using static zzre.Diagnostics;

namespace zzre.validation;

public class Validator(ITagContainer diContainer)
{
    private readonly ILogger logger = diContainer.GetLoggerFor<Validator>();
    private readonly IResourcePool resourcePool = diContainer.GetTag<IResourcePool>();
    private readonly IAssetRegistry assetRegistry = diContainer.GetTag<IAssetRegistry>();
    private readonly Stopwatch stopwatch = new();
    private readonly List<Diagnostic> diagnostics = [];
    private uint queuedFileCount;
    private uint processedFileCount;
    private uint faultyFileCount;

    public uint QueuedFileCount => queuedFileCount;
    public uint ProcessedFileCount => processedFileCount;
    public uint FaultyFileCount => faultyFileCount;
    public IReadOnlyList<Diagnostic> Diagnostics => diagnostics;

    public ushort MaxConcurrency { get; init; } = checked((ushort)Environment.ProcessorCount);

    public Task Run() => Run(CancellationToken.None);
    public async Task Run(CancellationToken ct)
    {
        queuedFileCount = processedFileCount = faultyFileCount = 0;

        stopwatch.Restart();
        logger.Information("Started.");
        var resourceTraversalBlock = new TransformManyBlock<IResourcePool, IResource>(TraverseResourcePool, new()
        {
            MaxDegreeOfParallelism = MaxConcurrency,
            CancellationToken = ct
        });
        var processResourceBlock = new ActionBlock<IResource>(ValidateResource, new()
        {
            BoundedCapacity = MaxConcurrency,
            MaxDegreeOfParallelism = MaxConcurrency,
            SingleProducerConstrained = true,
            CancellationToken = ct
        });
        resourceTraversalBlock.LinkTo(processResourceBlock, new()
        {
            PropagateCompletion = true
        });

        await resourceTraversalBlock.SendAsync(resourcePool);
        resourceTraversalBlock.Complete();
        await processResourceBlock.Completion;
        stopwatch.Stop();

        diagnostics.Sort();
    }

    public void LogSummary()
    {
        int countInfos = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Info);
        int countWarns = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        int countErrors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        int countIntErrors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.InternalError);
        logger.Information("Validation of {ProcessedFileCount} resources finished in {Elapsed}", processedFileCount, stopwatch.Elapsed);
        if (countInfos > 0)
            logger.Information($"Informations: {countInfos}");
        if (countWarns > 0)
            logger.Information($"    Warnings: {countWarns}");
        if (countErrors > 0)
            logger.Information($"      Errors: {countErrors}");
        if (countIntErrors > 0)
            logger.Information($" Int. Errors: {countIntErrors}");
    }

    private IEnumerable<IResource> TraverseResourcePool(IResourcePool pool)
    {
        if (pool.Root.Type is ResourceType.File)
        {
            Interlocked.Increment(ref queuedFileCount);
            yield return pool.Root;
            yield break;
        }
        var queue = new Queue<IResource>();
        queue.Enqueue(pool.Root);
        while (queue.TryDequeue(out var resource))
        {
            foreach (var file in resource.Files)
            {
                Interlocked.Increment(ref queuedFileCount);
                yield return file;
            }
            queue.EnsureCapacity(queue.Count + resource.Directories.Count());
            foreach (var dir in resource.Directories)
                queue.Enqueue(dir);
        }
    }

    private async Task ValidateResource(IResource resource)
    {
        bool wasIgnored = false;
        try
        {
            var ext = Path.GetExtension(resource.Name).ToLowerInvariant();
            switch (ext)
            {
                case ".bsp": await ValidateWorld(resource); break;
                case ".scn": await ValidateScene(resource); break;
                case ".bmp":
                case ".dds": await ValidateTexture(resource); break;
                case ".aed": await ValidateActor(resource); break;
                case ".ed": await ValidateEffect(resource); break;
                case ".dff": await ValidateClump(resource); break;
                case ".ska": await ValidateAnimation(resource); break;
                default:
                    AddDiagnostic(ValIgnoredDueToExtension(resource.Path.ToString(), ext));
                    wasIgnored = true;
                    break;
            }
        }
        catch (Exception e)
        {
            AddDiagnostic(ValGeneralException(resource.Path.ToString(), e));
            Interlocked.Increment(ref faultyFileCount);
        }
        finally
        {
            if (!wasIgnored)
                Interlocked.Increment(ref processedFileCount);
        }
    }

    private async Task ValidateWorld(IResource resource)
    {
        using var handle = assetRegistry.LoadWorld(resource.Path, AssetPriority.High);
        var world = await handle.GetAsync(CancellationToken.None);
        var collider = WorldCollider.Create(world.Mesh.World);
    }

    private async Task ValidateClump(IResource resource)
    {
        using var handle = assetRegistry.Load<ClumpAsset.Info, ClumpAsset>(new(resource.Path), AssetPriority.High);
        var world = await handle.GetAsync(CancellationToken.None);
        var collider = GeometryCollider.Create(world.Mesh.Geometry, location: null);
    }

    private async Task ValidateTexture(IResource resource)
    {
        using var handle = assetRegistry.LoadTexture(resource.Path, AssetPriority.High);
        await handle.GetAsync(CancellationToken.None);
    }

    private async Task ValidateScene(IResource resource)
    {
        using var handle = assetRegistry.LoadScene(resource.Path, AssetPriority.High);
        await handle.GetAsync(CancellationToken.None);
    }

    private async Task ValidateActor(IResource resource)
    {
        using var handle = assetRegistry.LoadActor(
            Path.GetFileNameWithoutExtension(resource.Name),
            AssetPriority.High);
        await handle.GetAsync(CancellationToken.None);
    }

    private async Task ValidateEffect(IResource resource)
    {
        using var handle = assetRegistry.LoadEffectCombiner(resource.Path, AssetPriority.High);
        await handle.GetAsync(CancellationToken.None);
    }

    private async Task ValidateAnimation(IResource resource)
    {
        using var handle = assetRegistry.LoadAnimation(resource.Name, AssetPriority.High);
        await handle.GetAsync(CancellationToken.None);
    }

    private void AddDiagnostic(Diagnostic diagnostic)
    {
        lock (diagnostics)
            diagnostics.Add(diagnostic);
    }
}
