﻿using System;
using System.Collections.Generic;
using System.Linq;
using Veldrid;
using ImGuiNET;
using static ImGuiNET.ImGui;
using System.Numerics;

namespace zzre.rendering
{
    public class IconFont : BaseDisposable
    {
        public Texture Texture { get; }
        public Sampler Sampler { get; }
        public IReadOnlyDictionary<string, Rect> Glyphs { get; }

        public unsafe IconFont(GraphicsDevice device, Action<ImFontAtlasPtr> addFontToAtlas)
        {
            var atlas = new ImFontAtlasPtr(ImGuiNative.ImFontAtlas_ImFontAtlas());
            addFontToAtlas(atlas);
            atlas.Flags = ImFontAtlasFlags.NoMouseCursors;
            if (!atlas.Build())
                throw new InvalidProgramException("Building ImFontAtlas failed");
            if (atlas.Fonts.Size != 1)
                throw new InvalidProgramException("ImFontAtlas does not contain a font after building");
            var font = *(ImFont**)atlas.Fonts.Data.ToPointer();
            if (font->Glyphs.Size <= 0)
                throw new InvalidProgramException("ImFont does not contain at least one glyph after building");

            IntPtr texPixels;
            int texWidth, texHeight;
            atlas.GetTexDataAsAlpha8(out texPixels, out texWidth, out texHeight);
            Texture = device.ResourceFactory.CreateTexture(new TextureDescription(
                width: (uint)texWidth,
                height: (uint)texHeight,
                format: PixelFormat.R8_UNorm,
                usage: TextureUsage.Sampled,
                type: TextureType.Texture2D,
                depth: 1,
                mipLevels: 1,
                arrayLayers: 1));
            Texture.Name = "IconFontTexture";
            device.UpdateTexture(Texture, texPixels, (uint)(texWidth * texHeight * 1), 0, 0, 0, (uint)texWidth, (uint)texHeight, 1, 0, 0);

            var glyphPtr = (ImFontGlyph*)font->Glyphs.Data.ToPointer();
            var glyphs = new Dictionary<string, Rect>();
            for (int i = 0; i < font->Glyphs.Size; i++)
            {
                var uvMin = new Vector2(glyphPtr[i].U0, glyphPtr[i].V1); // switch v to prevent vflip
                var uvMax = new Vector2(glyphPtr[i].U1, glyphPtr[i].V0);
                var codepoint = (int)(glyphPtr[i].Codepoint & ~0x80000000); // not sure why ImGui adds this bit?
                glyphs[char.ConvertFromUtf32(codepoint)] = new Rect((uvMin + uvMax) / 2, uvMax - uvMin);
            }
            Glyphs = glyphs;
            atlas.Destroy();

            Sampler = device.ResourceFactory.CreateSampler(new SamplerDescription(
                addressModeU: SamplerAddressMode.Border,
                addressModeV: SamplerAddressMode.Border,
                addressModeW: SamplerAddressMode.Border,
                filter: SamplerFilter.MinLinear_MagLinear_MipLinear,
                comparisonKind: null,
                maximumAnisotropy: 0,
                minimumLod: 0,
                maximumLod: 1,
                lodBias: 0,
                borderColor: SamplerBorderColor.TransparentBlack
                ));
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Texture.Dispose();
            Sampler.Dispose();
        }

        public static IconFont CreateForkAwesome(GraphicsDevice device, float size = 64.0f) =>
            new IconFont(device, atlas => zzre.core.assets.ForkAwesomeIconFont.AddToFontAtlas(atlas, 0, size, size));
    }
}