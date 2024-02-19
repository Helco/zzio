using System;
using Silk.NET.SDL;

using unsafe FnRWFromConstMem = delegate* unmanaged[Cdecl]<void*, int, Silk.NET.SDL.RWops*>;
using unsafe FnRWClose = delegate* unmanaged[Cdecl]<Silk.NET.SDL.RWops*, int>;

namespace zzre;

public unsafe struct SdlSurfacePtr(Sdl sdl, Surface* surface) : IDisposable
{
    public Surface* Surface { get; private set; } = surface;
    public readonly int Width => Surface == null
        ? throw new ObjectDisposedException(nameof(Surface))
        : Surface->W;
    public readonly int Height => Surface == null
        ? throw new ObjectDisposedException(nameof(Surface))
        : Surface->H;
    public readonly ReadOnlySpan<byte> Data => Surface == null
        ? throw new ObjectDisposedException(nameof(Surface))
        : new ReadOnlySpan<byte>(Surface->Pixels, Surface->Pitch * Surface->H);

    public void Dispose()
    {
        if (Surface != null)
        {
            sdl.FreeSurface(Surface);
            Surface = null;
        }
    }
}

public unsafe static class SdlExtensions
{
    // I am content with constricting Silk to a single context
    // also: look what SDL actually provides and we could have in Silk v_v

    private static FnRWFromConstMem rwFromConstMem;
    public static RWops* RWFromConstMem(this Sdl sdl, void* mem, int size)
    {
        if (rwFromConstMem is null)
            rwFromConstMem = (FnRWFromConstMem)sdl.Context.GetProcAddress("SDL_RWFromConstMem");
        return rwFromConstMem(mem, size);
    }

    public static RWops* RWFromConstMem(this Sdl sdl, ReadOnlySpan<byte> mem)
    {
        fixed (byte* ptr = mem)
            return sdl.RWFromConstMem(ptr, mem.Length);
    }

    private static FnRWClose rwClose;
    public static int RWClose(this Sdl sdl, RWops* context)
    {
        if (rwClose is null)
            rwClose = (FnRWClose)sdl.Context.GetProcAddress("SDL_RWClose");
        return rwClose(context);
    }

    public static void ThrowError(this Sdl sdl, string context)
    {
        var exception = sdl.GetErrorAsException();
        if (exception == null)
            throw new SdlException("Unknown SDL error during " + context);
        else
            throw exception;
    }
}
