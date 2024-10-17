using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Serilog;
using zzio.scn;
using zzio.vfs;

namespace zzre.validation;

public class Validator(ITagContainer diContainer)
{
    private readonly ILogger logger = diContainer.GetLoggerFor<Validator>();
    private readonly IResourcePool resourcePool = diContainer.GetTag<IResourcePool>();
    private readonly IAssetRegistry assetRegistry = diContainer.GetTag<IAssetRegistry>();
    private readonly Stopwatch stopwatch = new();
    private uint queuedFileCount;
    private uint processedFileCount;
    private uint faultyFileCount;

    public uint QueuedFileCount => queuedFileCount;
    public uint ProcessedFileCount => processedFileCount;
    public uint FaultyFileCount => faultyFileCount;

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
        logger.Information("Validation finished in {Elapsed}", stopwatch.Elapsed);
    }

    private IEnumerable<IResource> TraverseResourcePool(IResourcePool pool)
    {
        if (pool.Root.Type is ResourceType.File)
        {
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
        try
        {
            switch (Path.GetExtension(resource.Name).ToLowerInvariant())
            {
                case ".bsp": await ValidateWorld(resource); break;
                case ".scn": ValidateScene(resource); break;
                default:
                    logger.Verbose("Ignored file (due to extension): {FileName}", resource.Name);
                    break;
            }
        }
        catch (Exception e)
        {
            logger.Error("Exception when processing {Resource}: {Exception}", resource.Name, e);
            Interlocked.Increment(ref faultyFileCount);
        }
        finally
        {
            Interlocked.Increment(ref processedFileCount);
        }
    }

    private async Task ValidateWorld(IResource resource)
    {
        using var handle = assetRegistry.LoadWorld(resource.Path, AssetLoadPriority.High);
        await assetRegistry.WaitAsyncAll([ handle.Inner ]);
        var world = handle.Get();
        var collider = WorldCollider.Create(world.Mesh.World);
    }

    private void ValidateScene(IResource resource)
    {
        using var stream = resource.OpenContent() ??
            throw new IOException($"Could not open scene {resource.Name}");
        var scene = new Scene();
        scene.Read(stream);
    }
}
