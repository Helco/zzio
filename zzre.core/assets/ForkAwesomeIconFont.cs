using System;
using System.IO;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace zzre.core.assets;

public static unsafe class ForkAwesomeIconFont
{
    private static readonly Lazy<IntPtr> GlyphRangePtr = new(() =>
    {
        var ptr = Marshal.AllocHGlobal(sizeof(ushort) * 3);
        ushort* glyphRanges = (ushort*)ptr.ToPointer();
        glyphRanges[0] = IconFonts.ForkAwesome.IconMin;
        glyphRanges[1] = IconFonts.ForkAwesome.IconMax;
        glyphRanges[2] = 0;
        return ptr;
    });

    public static void AddToFontAtlas(ImFontAtlasPtr fontAtlas, byte mergeMode, float size, float minAdvanceX)
    {
        var assembly = typeof(ForkAwesomeIconFont).Assembly;
        using var stream = assembly.GetManifestResourceStream("zzre.core.assets.forkawesome-webfont.ttf");
        if (stream == null)
            throw new FileNotFoundException("Could not find embedded ForkAwesome font");
        var data = new byte[stream.Length];
        if (stream.Read(data, 0, data.Length) != data.Length)
            throw new EndOfStreamException("Could not read ForkAwesome font from resources");
        stream.Close();


        var fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
        fontConfig->MergeMode = mergeMode;
        fontConfig->GlyphMinAdvanceX = minAdvanceX;
        fontConfig->FontDataOwnedByAtlas = 0;
        fixed (byte* fontPtr = data)
        {
            fontAtlas.AddFontFromMemoryTTF(new IntPtr(fontPtr), data.Length, size, fontConfig, GlyphRangePtr.Value);
        }
        ImGuiNative.ImFontConfig_destroy(fontConfig);
    }
}
