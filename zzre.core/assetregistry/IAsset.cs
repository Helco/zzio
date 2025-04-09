using System;
using System.Threading.Tasks;

namespace zzre;

/// <summary>The loading state of an asset</summary>
public enum AssetState
{
    /// <summary>Loading of the asset has not been started yet</summary>
    /// <remarks>E.g. Low prioritised asset loading is started across several frames</remarks>
    Queued,
    /// <summary>The asset is currently loading</summary>
    Loading,
    /// <summary>The primary asset is loaded, but needs to wait for secondary asset loading to finish</summary>
    LoadingSecondary,
    /// <summary>The asset has been completly loaded and is ready to use</summary>
    /// <remarks>This state does not entail applying the asset</remarks>
    Loaded,
    /// <summary>The asset has been disposed of and should not be used anymore</summary>
    Disposed,
    /// <summary>There was some error during loading and the asset should not be used</summary>
    Error
}

/// <summary>The internal interface into an asset</summary>
internal interface IAsset
{
    /// <summary>An unique identifier given by the registry related to the Info value in order to efficiently address an asset instance</summary>
    /// <remarks>The ID is chosen randomly and will change at least per process per asset</remarks>
    Guid ID { get; }

     /// <summary>The current loading state of the asset</summary>
    AssetState State { get; }

    /// <summary>The priority that the asset was *effectively* loaded with</summary>
    AssetLoadPriority Priority { get; }

    FFTask LoadTask { get; }

    /// <summary>The current reference count to be modified to keep assets alive or disposing them</summary>
    /// <remarks>Modifying has to be done using the <see cref="AddRef"/> and <see cref="DelRef"/> methods</remarks>
    int RefCount { get; }

    /// <summary>Starts the loading of the asset on the thread pool</summary>
    /// <remarks>This call is ignored if the <see cref="State"/> is not <see cref="AssetState.Queued"/></remarks>
    void StartLoading(AssetLoadPriority priority);

    /// <summary>Increases the reference count</summary>
    void AddRef();

    /// <summary>Decreases the reference count and disposes the asset if necessary</summary>
    /// <remarks>It will also signal a disposal to the registry</remarks>
    void DelRef();

    /// <summary>Rethrows a loading exception that already occured</summary>
    /// <remarks>Assumes that the load task has already completed with an error</remarks>
    void ThrowIfError();

    void AddApplyAction(Action<AssetHandle>? action);

    Task AddApplyActionAsyc(Action<AssetHandle>? action);

    void ExecuteApplyActions(AssetHandle handle);

    ValueTask<FFTaskStatus> Complete(AssetLoadPriority priority);

    void Dispose();
}
