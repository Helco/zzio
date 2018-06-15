using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace zzio {
    namespace rwbs {
        public partial class Reader {
            private string readZString(BinaryReader reader) {
                UInt32 len = reader.ReadUInt32();
                byte[] buf = reader.ReadBytes((Int32)len);
                return Encoding.UTF8.GetString(buf).Replace("\u0000", "");
            }

            private RWString readString(ListSection p, MemoryStream stream, int size) {
                byte[] buffer = new byte[size];
                stream.Read(buffer, 0, size);
                return new RWString(p, Encoding.UTF8.GetString(buffer).Replace("\u0000", ""));
            }

            private RWTexture readTexture(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                TextureFilterMode fm = zzio.Utils.intToEnum<TextureFilterMode>(reader.ReadByte());
                byte address = reader.ReadByte();
                TextureAddressingMode
                    uam = zzio.Utils.intToEnum<TextureAddressingMode>(address & 0xf),
                    vam = zzio.Utils.intToEnum<TextureAddressingMode>(address >> 4);
                UInt16 flags = reader.ReadUInt16();
                return new RWTexture(p, fm, uam, vam, (flags & 1) > 0);
            }

            private RWMaterial readMaterial(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                return new RWMaterial(p,
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadInt32() > 0,
                    reader.BaseStream.Length > 16 ? reader.ReadSingle() : 0.0f,
                    reader.BaseStream.Length > 20 ? reader.ReadSingle() : 0.0f,
                    reader.BaseStream.Length > 24 ? reader.ReadSingle() : 0.0f
                    );
            }

            private RWMaterialList readMaterialList(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                UInt32 matCount = reader.ReadUInt32();
                Int32[] matIndices = new Int32[matCount];
                for (UInt32 i = 0; i < matCount; i++)
                    matIndices[i] = reader.ReadInt32();
                return new RWMaterialList(p, matIndices);
            }

            private RWAtomicSection readAtomicSection(ListSection p, RWStruct str) {
                RWWorld world = (RWWorld)(p.sectionId == SectionId.World ? p : p.getParentById(SectionId.World));
                BinaryReader reader = str.getBinaryReader();
                UInt32 matIdBase = reader.ReadUInt32();
                UInt32 triCount = reader.ReadUInt32();
                UInt32 vertexCount = reader.ReadUInt32();
                Vector bbox1 = Vector.read(reader), bbox2 = Vector.read(reader);
                UInt32 unused1 = reader.ReadUInt32(), unused2 = reader.ReadUInt32();
                Vector[] vertices;
                Normal[] normals = null;
                UInt32[] colors = null;
                TexCoord[] texCoords1 = null, texCoords2 = null;
                Triangle[] triangles;

                vertices = new Vector[vertexCount];
                for (UInt32 i = 0; i < vertexCount; i++)
                    vertices[i] = Vector.read(reader);

                if ((world.format & GeometryFormat.Normals) > 0) {
                    normals = new Normal[vertexCount];
                    for (UInt32 i = 0; i < vertexCount; i++)
                        normals[i] = Normal.read(reader);
                }

                if ((world.format & GeometryFormat.Prelit) > 0) {
                    colors = new UInt32[vertexCount];
                    for (UInt32 i = 0; i < vertexCount; i++)
                        colors[i] = reader.ReadUInt32();
                }

                if ((world.format & (GeometryFormat.Textured | GeometryFormat.Textured2)) > 0) {
                    texCoords1 = new TexCoord[vertexCount];
                    for (UInt32 i = 0; i < vertexCount; i++)
                        texCoords1[i] = TexCoord.read(reader);
                }
                if ((world.format & GeometryFormat.Textured2) > 0) {
                    texCoords2 = new TexCoord[vertexCount];
                    for (UInt32 i = 0; i < vertexCount; i++)
                        texCoords2[i] = TexCoord.read(reader);
                }

                triangles = new Triangle[triCount];
                for (UInt32 i = 0; i < triCount; i++)
                    triangles[i] = Triangle.read(reader);

                return new RWAtomicSection(p, matIdBase, bbox1, bbox2, unused1, unused2, vertices,
                    normals, colors, texCoords1, texCoords2, triangles);
            }

            private RWPlaneSection readPlaneSection(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                return new RWPlaneSection(p,
                    reader.ReadUInt32(),
                    reader.ReadSingle(),
                    reader.ReadUInt32() > 0,
                    reader.ReadUInt32() > 0,
                    reader.ReadSingle(),
                    reader.ReadSingle()
                    );
            }

            private RWWorld readWorld(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                return new RWWorld(p,
                    reader.ReadUInt32() > 0,
                    Vector.read(reader),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    (GeometryFormat)reader.ReadUInt32()
                    );
            }
        }
    }
}