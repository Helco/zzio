using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DefaultEcs.Resource;
using Silk.NET.OpenAL;
using Silk.NET.OpenAL.Extensions.EXT;
using Silk.NET.SDL;
using zzio.vfs;

namespace zzre.game.resources;

public sealed class Sound : AResourceManager<string, components.SoundBuffer>
{
    private readonly ITagContainer diContainer;
    private readonly IResourcePool resourcePool;
    private readonly Sdl sdl;
    private readonly OpenALDevice? device;
    private readonly List<uint> buffersToDelete = new(8);
    // Unfortunately the order of source cleanup and buffer deletion is important,
    // alDeleteBuffers will return with InvalidOperation and not delete the buffer
    // if it is still in use.
    // so instead we get hacky and delay the actual deletion for a bit

    public Sound(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        resourcePool = diContainer.GetTag<IResourcePool>();
        sdl = diContainer.GetTag<Sdl>();
        diContainer.TryGetTag<OpenALDevice>(out device);
        Manage(diContainer.GetTag<DefaultEcs.World>());
    }

    protected override components.SoundBuffer Load(string path)
    {
        if (device == null || !diContainer.HasTag<systems.SoundContext>())
            return default;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".wav" => LoadWave(path),
            ".mp3" => LoadMP3(path),
            _ => throw new NotSupportedException($"Unsupport sound extension: {ext}")
        };
    }

    private unsafe components.SoundBuffer LoadWave(string path)
    {
        using var stream = resourcePool.FindAndOpen(path) ??
            throw new FileNotFoundException("Could not open sound: " + path);
        var fileBuffer = new byte[stream.Length];
        stream.ReadExactly(fileBuffer.AsSpan());
        var rwops = sdl.RWFromConstMem(fileBuffer);

        AudioSpec audioSpec = default;
        byte* audioBuf = null;
        uint audioBufLen = 0;
        if (sdl.LoadWAVRW(rwops, freesrc: 1, &audioSpec, &audioBuf, &audioBufLen) is null)
            sdl.ThrowError($"LoadWAV failed for unknown reason: {path}");

        try
        {
            var bufferId = device!.AL.GenBuffer();
            device.AL.BufferData(bufferId, audioSpec.Format switch
            {
                Sdl.AudioU8 when audioSpec.Channels == 1 => BufferFormat.Mono8,
                Sdl.AudioU8 when audioSpec.Channels == 2 => BufferFormat.Stereo8,
                Sdl.AudioS16Lsb when audioSpec.Channels == 1 => BufferFormat.Mono16,
                Sdl.AudioS16Lsb when audioSpec.Channels == 2 => BufferFormat.Stereo16,
                _ => throw new NotSupportedException($"Unsupported audio format {audioSpec.Format}, {audioSpec.Channels} channels")
            }, audioBuf, (int)audioBufLen, audioSpec.Freq);
            device.AL.ThrowOnError();
            return new(bufferId);
        }
        finally
        {
            if (audioBuf != null)
                sdl.FreeWAV(audioBuf);
        }
    }

    private components.SoundBuffer LoadMP3(string path)
    {
        using var stream = resourcePool.FindAndOpen(path) ??
            throw new FileNotFoundException("Could not open sound: " + path);
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

        var bufferId = device!.AL.GenBuffer();
        device.AL.BufferData(bufferId, format, samples, mpegFile.SampleRate);
        device.AL.ThrowOnError();
        return new(bufferId);
    }

    protected override void Unload(string info, components.SoundBuffer resource)
    {
        buffersToDelete.Add(resource.Id);
    }

    protected override void OnResourceLoaded(in DefaultEcs.Entity entity, string info, components.SoundBuffer resource)
    {
        entity.Set(resource);
    }

    public unsafe void RegularCleanup()
    {
        if (buffersToDelete.Count == 0 || device == null ||
            !diContainer.TryGetTag<systems.SoundContext>(out var context))
            return;
        using var _ = context.EnsureIsCurrent();
        var buffersToDeleteSpan = CollectionsMarshal.AsSpan(buffersToDelete);
        fixed (uint* buffersPtr = buffersToDeleteSpan)
            device.AL.DeleteBuffers(buffersToDelete.Count, buffersPtr);
        buffersToDelete.Clear();
    }
}
