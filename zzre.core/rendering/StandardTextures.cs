using System;
using Veldrid;
using zzio;

namespace zzre;

public enum StandardTextureKind
{
    White,
    Black,
    Clear,
    Error
}

public sealed class StandardTextures : IDisposable
{
    // please don't dispose any of these textures, mkay?

    private readonly GraphicsDevice graphicsDevice;
    private readonly ResourceFactory resourceFactory;

    public StandardTextures(ITagContainer diContainer)
    {
        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        resourceFactory = diContainer.GetTag<ResourceFactory>();
        white = new(() => MakeSinglePixel("Standard White", IColor.White), isThreadSafe: true);
        black = new(() => MakeSinglePixel("Standard Black", IColor.Black), isThreadSafe: true);
        clear = new(() => MakeSinglePixel("Standard Clear", IColor.Clear), isThreadSafe: true);
        error = new(MakeError, isThreadSafe: true);
    }

    private unsafe Texture MakeSinglePixel(string name, IColor color)
    {
        var texture = graphicsDevice.ResourceFactory.CreateTexture(
            new(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
        texture.Name = name;
        var bytes = stackalloc byte[4];
        *((uint*)bytes) = color.Raw;
        graphicsDevice.UpdateTexture(texture, (nint)bytes, 4, 0, 0, 0, 1, 1, 1, 0, 0);
        return texture;
    }

    private unsafe Texture MakeError()
    {
        var texture = resourceFactory.CreateTexture(
            new(2, 2, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
        graphicsDevice.UpdateTexture(texture, new byte[]
        {
                0xff, 0x00, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff,
                0x00, 0x00, 0x00, 0xff,
                0xff, 0x00, 0xff, 0xff
        }, 0, 0, 0, 2, 2, 1, 0, 0);
        texture.Name = "Standard Error";
        return texture;
    }

    private readonly Lazy<Texture> white;
    private readonly Lazy<Texture> black;
    private readonly Lazy<Texture> clear;
    private readonly Lazy<Texture> error;
    private bool disposedValue;

    public Texture White => GetFrom(white);
    public Texture Black => GetFrom(black);
    public Texture Clear => GetFrom(clear);
    public Texture Error => GetFrom(error);

    public Texture ByKind(StandardTextureKind kind) => GetFrom(kind switch
    {
        StandardTextureKind.White => white,
        StandardTextureKind.Black => black,
        StandardTextureKind.Clear => clear,
        StandardTextureKind.Error => error,
        _ => throw new ArgumentException($"Unsupported standard texture kind {kind}", nameof(kind))
    });

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                DisposeIfCreated(white);
                DisposeIfCreated(black);
                DisposeIfCreated(clear);
                DisposeIfCreated(error);
            }
            disposedValue = true;
        }
    }
    
    private static Texture GetFrom(Lazy<Texture> lazyTexture)
    {
        var texture = lazyTexture.Value;
        ObjectDisposedException.ThrowIf(texture.IsDisposed, texture);
        return texture;
    }

    private static void DisposeIfCreated(Lazy<Texture> texture)
    {
        if (texture.IsValueCreated)
            texture.Value.Dispose();
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
