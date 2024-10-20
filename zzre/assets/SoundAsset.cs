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
        FixTruncatedWave(ref fileBuffer);
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

    private const int RIFFSizeWithFormatChunk = 0x24;
    private const uint FourCCRIFF = 0x46464952u;
    private const uint FourCCWAVE = 0x45564157u;
    private const uint FourCCfmt = 0x20746D66u;
    private const uint FourCCfact = 0x74636166;
    private const uint FourCCdata = 0x61746164;
    private void FixTruncatedWave(ref byte[] original)
    {
        /* Some of the ADPCM encoded wave files in Zanzarah have truncated data blocks meaning
         * the data chunk size does not adhere to the reported alignment and the file might
         * be too small.
         * SDL reacts by dropping the last chunk of audio data.
         * We instead round the block size up to the next alignment and grow the buffer with zeros.
         * Also we delete the fact chunk
         */

        if (original.Length < RIFFSizeWithFormatChunk)
            throw new InvalidDataException("WAVE file is too small");
        if (BitConverter.ToUInt32(original, 0) != FourCCRIFF ||
            BitConverter.ToUInt32(original, 8) != FourCCWAVE ||
            BitConverter.ToUInt32(original, 12) != FourCCfmt)
            throw new InvalidDataException("Given buffer can not be recognized as a wav file");
        if (BitConverter.ToUInt16(original, 0x14) != 17)
            return; // We have only heard ADPCM encoded sounds that are cut off
        int blockAlign = BitConverter.ToUInt16(original, 0x20);

        int endOfFmtChunk = 20 + BitConverter.ToInt32(original, 16);
        int curBlock = endOfFmtChunk;
        if (BitConverter.ToUInt32(original, curBlock) == FourCCfact)
            curBlock += 8 + BitConverter.ToInt32(original, curBlock + 4);

        if (curBlock + 8 >= original.Length)
            throw new InvalidDataException("WAVE file is too small to contain data chunk");
        if (BitConverter.ToUInt32(original, curBlock) != FourCCdata)
            throw new InvalidDataException("Did not find data chunk in WAVE file");

        int dataSize = BitConverter.ToInt32(original, curBlock + 4);
        if (dataSize % blockAlign == 0)
            return;

        int newDataSize = (dataSize + blockAlign * 2);
        newDataSize -= newDataSize % blockAlign;
        BitConverter.GetBytes(newDataSize).CopyTo(original, curBlock + 4);

        int newBufferSize = curBlock + 8 + newDataSize;
        if (newBufferSize > original.Length)
            Array.Resize(ref original, newBufferSize);

        diContainer.GetLoggerFor<SoundAsset>().Verbose("Fixed truncated WAVE file (adding {Bytes} bytes): {Path}", newDataSize - dataSize, info.FullPath);
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

    protected override string ToStringInner() => $"SoundAsset {info.FullPath}";
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
