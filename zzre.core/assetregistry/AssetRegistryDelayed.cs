using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace zzre;

public sealed class AssetRegistryDelayed : IAssetRegistryInternal
{
    [ExcludeFromCodeCoverage]
    public bool WasDisposed => Inner.WasDisposed;
    [ExcludeFromCodeCoverage]
    public bool IsMainThread => Inner.IsMainThread;
    [ExcludeFromCodeCoverage]
    public ITagContainer DIContainer => Inner.DIContainer;
    [ExcludeFromCodeCoverage]
    public IAssetRegistry? ParentRegistry => Inner.ParentRegistry;
    [ExcludeFromCodeCoverage]
    public bool IsLocalRegistry => Inner.IsLocalRegistry;
    [ExcludeFromCodeCoverage]
    public CancellationToken Cancellation => Inner.Cancellation;
    [ExcludeFromCodeCoverage]
    public AssetRegistryStats Stats => Inner.Stats;

    [ExcludeFromCodeCoverage]
    public void CopyDebugInfo(List<IAssetRegistry.AssetInfo> assetInfos) =>
        Inner.CopyDebugInfo(assetInfos);

    [ExcludeFromCodeCoverage]
    public void Dispose() => Inner.Dispose();
    [ExcludeFromCodeCoverage]
    public void Update() => Inner.Update();

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.AddRef(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).AddRef(assetId);

    [ExcludeFromCodeCoverage]
    public void Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action)
        where TAsset : class, IAsset =>
        Inner.Apply(handle, action);

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.CheckType(Guid assetId, Type type) =>
        ((IAssetRegistryInternal)Inner).CheckType(assetId, type);

    [ExcludeFromCodeCoverage]
    FFTask<IDisposable> IAssetRegistryInternal.GetAsset(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).GetAsset(assetId);

    [ExcludeFromCodeCoverage]
    public bool TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle)
        where TAsset : class, IAsset =>
        Inner.TryGet(assetId, out handle);

    public bool DelayDisposals
    {
        get => Volatile.Read(ref delayDeletion);
        set
        {
            Volatile.Write(ref delayDeletion, value);
            if (!value || Inner.WasDisposed)
                return;
            lock (assetIdsToDelete)
            {
                foreach (var id in assetIdsToDelete)
                    ((IAssetRegistryInternal)Inner).DelRef(id);
                assetIdsToDelete.Clear();
            }
        }
    }

    public readonly IAssetRegistry Inner;
    private readonly GlobalRegistryAdapter globalAdapter;
    private readonly List<Guid> assetIdsToDelete = new(64);
    private bool delayDeletion;

    public AssetRegistryDelayed(IAssetRegistry inner)
    {
        Inner = inner;
        globalAdapter = new(this);
    }

    public AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        var parentHandle = Inner.Load<TInfo, TAsset>(info, priority);
        Debug.Assert(parentHandle.Asset == parentHandle.Asset); // checks that the handle is not disposed
        Debug.Assert(parentHandle.Registry == Inner || (parentHandle.Registry == Inner.ParentRegistry && Inner.ParentRegistry is not null));
        return parentHandle.Registry == Inner.ParentRegistry
            ? new(globalAdapter, parentHandle.AssetId, false)
            : new(this, parentHandle.AssetId, false);
    }

    void IAssetRegistryInternal.DelRef(Guid assetId)
    {
        if (DelayDisposals)
        {
            lock (assetIdsToDelete)
                assetIdsToDelete.Add(assetId);
        }
        else
            ((IAssetRegistryInternal)Inner).DelRef(assetId);
    }

    private sealed class GlobalRegistryAdapter(AssetRegistryDelayed Parent) : IAssetRegistryInternal
    {
        // Reference counting has to be done by delayed registry
        public void AddRef(Guid assetId) => ((IAssetRegistryInternal)Parent).AddRef(assetId);
        public void DelRef(Guid assetId) => ((IAssetRegistryInternal)Parent).DelRef(assetId);

        // Asset retrieval has to use the *global* registry
        public FFTask<IDisposable> GetAsset(Guid assetId) => ((IAssetRegistryInternal)Parent.ParentRegistry!).GetAsset(assetId);

        // Pure referals or exceptions
        public bool WasDisposed => Parent.WasDisposed;
        public bool IsMainThread => Parent.IsMainThread;
        public ITagContainer DIContainer => Parent.DIContainer;
        public IAssetRegistry? ParentRegistry => Parent.ParentRegistry;
        public bool IsLocalRegistry => Parent.IsLocalRegistry;
        public CancellationToken Cancellation => Parent.Cancellation;
        public AssetRegistryStats Stats => Parent.Stats;

        private static void ThrowUnexpectedUsage() =>
            throw new InvalidOperationException("The global adapter of a delayed registry cannot be used for this operation");

        public void CopyDebugInfo(List<IAssetRegistry.AssetInfo> assetInfos) => ThrowUnexpectedUsage();
        public void CheckType(Guid assetId, Type type) => ThrowUnexpectedUsage();
        public void Dispose() => ThrowUnexpectedUsage();
        public void Update() => ThrowUnexpectedUsage();

        void IAssetRegistry.Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action) => ThrowUnexpectedUsage();

        AssetHandle<TAsset> IAssetRegistry.Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        {
            ThrowUnexpectedUsage();
            return default;
        }

        bool IAssetRegistry.TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle)
        {
            ThrowUnexpectedUsage();
            handle = default;
            return default;
        }
    }
}
