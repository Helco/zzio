using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    public enum TextureFilterMode
    {
        NAFilterMode = 0,
        Nearest = 1,
        Linear = 2,
        MipNearest = 3,
        MipLinear = 4,
        LinearMipNearest = 5,
        LinearMipLinear = 6,

        Unknown = -1
    }

    public enum TextureAddressingMode
    {
        NATextureAddress = 0,
        Wrap = 1,
        Mirror = 2,
        Clamp = 3,
        Border = 4,

        Unknown = -1
    }

    [Serializable]
    public class RWTexture : StructSection
    {
        public override SectionId sectionId { get { return SectionId.Texture; } }

        public TextureFilterMode filterMode;
        public TextureAddressingMode uAddressingMode;
        public TextureAddressingMode vAddressingMode;
        public bool useMipLevels;

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            filterMode = EnumUtils.intToEnum<TextureFilterMode>(reader.ReadByte());
            byte addressing = reader.ReadByte();
            uAddressingMode = EnumUtils.intToEnum<TextureAddressingMode>(addressing & 0xf);
            vAddressingMode = EnumUtils.intToEnum<TextureAddressingMode>(addressing >> 4);
            UInt16 flags = reader.ReadUInt16();
            useMipLevels = (flags & 1) > 0;
            // more flags are not known yet
        }
        
        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write((byte)filterMode);
            byte addressing = (byte)(
                ((((int)uAddressingMode) & 0xf) << 0) |
                ((((int)vAddressingMode) & 0xf) << 4)
            );
            writer.Write(addressing);
            writer.Write((UInt16)(useMipLevels ? 1 : 0));
        }
    }
}
