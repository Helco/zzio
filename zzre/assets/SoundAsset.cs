using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;
using Silk.NET.SDL;
using zzio;
using zzio.vfs;
using zzre.game.systems;

namespace zzre;

public sealed class SoundAsset : Asset
{
    public readonly record struct Info(FilePath FullPath);

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<SoundAsset>(AssetLocality.Context);

    private readonly Info info;
    private uint? buffer;

    public uint Buffer => buffer ??
        throw new InvalidOperationException("Asset was not yet loaded");

    public SoundAsset(IAssetRegistry registry, Guid assetId, Info info) : base(registry, assetId)
    {
        this.info = info;
    }

    protected override ValueTask<IEnumerable<AssetHandle>> Load()
    {
        if (!diContainer.TryGetTag(out OpenALDevice device) ||
            !diContainer.TryGetTag(out SoundContext context))
        {
            buffer = 0; // A valid but unusable state
            return NoSecondaryAssets;
        }

        using var _ = context.EnsureIsCurrent();
        var ext = Path.GetExtension(info.FullPath.Parts[^1]).ToLowerInvariant();
        switch(ext)
        {
            case ".wav": LoadWave(device); break;
            case ".mp3": LoadMP3(device); break;
            default: throw new NotSupportedException($"Unsupported sound extension: {ext}");
        }
        return NoSecondaryAssets;
    }

    private unsafe void LoadWave(OpenALDevice device)
    {
        var sdl = diContainer.GetTag<Sdl>();
        var resourcePool = diContainer.GetTag<IResourcePool>();
        var fileBuffer = resourcePool.FindAndRead(info.FullPath) ??
            throw new FileNotFoundException("Could not open sound: " + info.FullPath);
        var rwops = sdl.RWFromConstMem(fileBuffer);

        AudioSpec audioSpec = default;
        byte* audioBuf = null;
        uint audioBufLen = 0;
        if (sdl.LoadWAVRW(rwops, freesrc: 1, &audioSpec, &audioBuf, &audioBufLen) is null)
            sdl.ThrowError($"LoadWAV failed for unknown reason: {info.FullPath}");

        try
        {
            var buffer = device!.AL.GenBuffer();
            device.AL.BufferData(buffer, audioSpec.Format switch
            {
                Sdl.AudioU8 when audioSpec.Channels == 1 => BufferFormat.Mono8,
                Sdl.AudioU8 when audioSpec.Channels == 2 => BufferFormat.Stereo8,
                Sdl.AudioS16Lsb when audioSpec.Channels == 1 => BufferFormat.Mono16,
                Sdl.AudioS16Lsb when audioSpec.Channels == 2 => BufferFormat.Stereo16,
                _ => throw new NotSupportedException($"Unsupported audio format {audioSpec.Format}, {audioSpec.Channels} channels")
            }, audioBuf, (int)audioBufLen, audioSpec.Freq);
            device.AL.ThrowOnError();
            this.buffer = buffer;
        }
        finally
        {
            if (audioBuf != null)
                sdl.FreeWAV(audioBuf);
        }
    }

    private void LoadMP3(OpenALDevice device)
    {
        var resourcePool = diContainer.GetTag<IResourcePool>();
        using var stream = resourcePool.FindAndOpen(info.FullPath) ??
            throw new FileNotFoundException("Could not open sound: " + info.FullPath);
        var mpegFile = new NLayer.MpegFile(stream, leaveOpen: true);
        var format = mpegFile.Channels switch
        {
            1 => FloatBufferFormat.Mono,
            2 => FloatBufferFormat.Stereo,
            _ => throw new NotSupportedException($"Unsupported MP3 file with {mpegFile.Channels} channels")
        };
        var samples = new float[mpegFile.Length ??
            throw new InvalidDataException("Cannot determine sample count")];
        int sampleCount = mpegFile.ReadSamples(samples);
        if (sampleCount != samples.Length)
            device!.Logger.Warning("Could not read {Intended} samples as intended from MP3 but only {Actual}", samples.Length, sampleCount);

        var buffer = device!.AL.GenBuffer();
        device.AL.BufferData(buffer, format, samples, mpegFile.SampleRate);
        device.AL.ThrowOnError();
        this.buffer = buffer;
    }

    protected override void Unload()
    {
        // We cannot just delete the buffer directly as the emitter disposal might not 
        // have gone through yet, so we defer the disposal until later (usually next frame)
        if (buffer is not (null or 0) && diContainer.TryGetTag(out SoundContext context))
            context.AddBufferDisposal(buffer.Value);
        buffer = null;
    }

    public override string ToString() => $"SoundAsset {info.FullPath}";
}

partial class AssetExtensions
{
    public unsafe static AssetHandle<SoundAsset> LoadSound(this IAssetRegistry registry,
        DefaultEcs.Entity entity,
        FilePath path,
        AssetLoadPriority priority)
    {
        var handle = registry.Load(new SoundAsset.Info(path), priority, &ApplySoundAssetToEntity, entity);
        entity.Set(handle);
        return handle.As<SoundAsset>();
    }

    private static void ApplySoundAssetToEntity(AssetHandle handle, ref readonly DefaultEcs.Entity entity)
    {
        if (entity.IsAlive)
            entity.Set(new game.components.SoundBuffer(handle.Get<SoundAsset>().Buffer));
    }
}
