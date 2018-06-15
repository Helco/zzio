using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace zzio {
    namespace rwbs {
        public partial class Reader {
            private RWFrameList readFrameList(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                UInt32 frameCount = reader.ReadUInt32();
                Frame[] frames = new Frame[frameCount];
                for (UInt32 i = 0; i < frameCount; i++)
                    frames[i] = Frame.read(reader);
                return new rwbs.RWFrameList(p, frames);
            }

            private RWGeometry readGeometry(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                Int32 formatInt;
                GeometryFormat format = (GeometryFormat)(formatInt = reader.ReadInt32());
                UInt32 triCount = reader.ReadUInt32();
                UInt32 vertexCount = reader.ReadUInt32();
                UInt32 morphTargetCount = reader.ReadUInt32();
                float ambient = reader.ReadSingle();
                float specular = reader.ReadSingle();
                float diffuse = reader.ReadSingle();

                UInt32[] colors = null;
                TexCoord[,] texCoords = null;
                Triangle[] triangles = null;
                if ((format & GeometryFormat.Native) == 0) {
                    if ((format & GeometryFormat.Prelit) > 0) {
                        colors = new UInt32[vertexCount];
                        for (UInt32 i = 0; i < vertexCount; i++)
                            colors[i] = reader.ReadUInt32();
                    }

                    if ((format & (GeometryFormat.Textured | GeometryFormat.Textured2)) > 0) {
                        Int32 texCount = (formatInt >> 16) & 0xff;
                        if (texCount == 0)
                            texCount = ((format & GeometryFormat.Textured) > 0 ? 1 : 2);
                        texCoords = new TexCoord[texCount, vertexCount];
                        for (UInt32 i=0; i<texCount; i++) {
                            for (UInt32 j = 0; j < vertexCount; j++)
                                texCoords[i, j] = TexCoord.read(reader);
                        }
                    }

                    triangles = new Triangle[triCount];
                    for (UInt32 i=0; i<triCount; i++) {
                        //note the different order here :(
                        triangles[i].v2 = reader.ReadUInt16();
                        triangles[i].v1 = reader.ReadUInt16();
                        triangles[i].m = reader.ReadUInt16();
                        triangles[i].v3 = reader.ReadUInt16();
                    }
                }

                RWGeometry.MorphTarget[] morphTargets = null;
                if (morphTargetCount > 0) {
                    morphTargets = new RWGeometry.MorphTarget[morphTargetCount];
                    for (UInt32 i=0; i<morphTargetCount; i++) {
                        morphTargets[i].bsphereCenter = Vector.read(reader);
                        morphTargets[i].bsphereRadius = reader.ReadSingle();
                        morphTargets[i].normals = morphTargets[i].vertices = null;
                        bool hasVertices = reader.ReadUInt32() > 0;
                        bool hasNormals = reader.ReadUInt32() > 0;
                        if (hasVertices) {
                            morphTargets[i].vertices = new Vector[vertexCount];
                            for (UInt32 j = 0; j < vertexCount; j++)
                                morphTargets[i].vertices[j] = Vector.read(reader);
                        }
                        if (hasNormals) {
                            morphTargets[i].normals = new Vector[vertexCount];
                            for (UInt32 j = 0; j < vertexCount; j++)
                                morphTargets[i].normals[j] = Vector.read(reader);
                        }
                    }
                }

                return new RWGeometry(p, format, ambient, specular, diffuse, colors, texCoords, triangles, morphTargets);
            }

            private RWClump readClump(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                UInt32 atomicCount = reader.ReadUInt32(),
                    lightCount = 0, camCount = 0;
                try {
                    lightCount = reader.ReadUInt32();
                    camCount = reader.ReadUInt32();
                }
                catch (EndOfStreamException) { }
                return new RWClump(p, atomicCount, lightCount, camCount);
            }

            private RWAtomic readAtomic(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                return new RWAtomic(p,
                    reader.ReadUInt32(),
                    reader.ReadUInt32(),
                    (AtomicFlags)reader.ReadInt32(),
                    reader.BaseStream.Position == reader.BaseStream.Length ? 0 : reader.ReadUInt32()
                    );
            }

            private RWGeometryList readGeometryList(ListSection p, RWStruct str) {
                BinaryReader reader = str.getBinaryReader();
                return new rwbs.RWGeometryList(p, reader.ReadUInt32());
            }

            private RWMorphPLG readMorphPLG(ListSection p, BinaryReader reader) {
                return new rwbs.RWMorphPLG(p, reader.ReadUInt32());
            }

            private RWSkinPLG readSkinPLG(ListSection p, BinaryReader reader) {
                UInt32 boneCount = reader.ReadUInt32();
                UInt32 vertexCount = reader.ReadUInt32();
                byte[,] vertexIndices = new byte[vertexCount, 4];
                float[,] vertexWeights = new float[vertexCount, 4];
                RWSkinPLG.Bone[] bones = new RWSkinPLG.Bone[boneCount];

                for (UInt32 i=0; i<vertexCount; i++) {
                    for (UInt32 j = 0; j < 4; j++)
                        vertexIndices[i, j] = reader.ReadByte();
                }
                for (UInt32 i=0; i<vertexCount; i++) {
                    for (UInt32 j = 0; j < 4; j++)
                        vertexWeights[i, j] = reader.ReadSingle();
                }
                for (UInt32 i=0; i<boneCount; i++) {
                    bones[i].id = reader.ReadUInt32();
                    bones[i].idx = reader.ReadUInt32();
                    bones[i].flags = zzio.Utils.intToEnum<BoneFlags>(reader.ReadInt32());

                    bones[i].right = Vector.read(reader);
                    bones[i].p1 = reader.ReadUInt32();
                    bones[i].up = Vector.read(reader);
                    bones[i].p2 = reader.ReadUInt32();
                    bones[i].at = Vector.read(reader);
                    bones[i].p3 = reader.ReadUInt32();
                    bones[i].pos = Vector.read(reader);
                    bones[i].p4 = reader.ReadUInt32();
                }

                return new RWSkinPLG(p, vertexIndices, vertexWeights, bones);
            }

            private RWBinMeshPLG readBinMeshPLG(ListSection p, BinaryReader reader) {
                BinMeshFlags flags = zzio.Utils.intToEnum<BinMeshFlags>(reader.ReadInt32());
                UInt32 meshCount = reader.ReadUInt32();
                UInt32 totalIndexCount = reader.ReadUInt32();
                RWBinMeshPLG.SubMesh[] meshes = new RWBinMeshPLG.SubMesh[meshCount];
                for (UInt32 i = 0; i < meshCount; i++) {
                    UInt32 indexCount = reader.ReadUInt32();
                    meshes[i].matIndex = reader.ReadUInt32();
                    meshes[i].indices = new UInt32[indexCount];
                    for (UInt32 j = 0; j < indexCount; j++)
                        meshes[i].indices[j] = reader.ReadUInt32();
                }
                return new RWBinMeshPLG(p, flags, totalIndexCount, meshes);
            }

            private RWAnimationPLG readAnimationPLG(ListSection p, BinaryReader reader) {
                //I am apologizing before even writing this...

                Int32 boneId = reader.ReadInt32();
                bool hasSubData = false;
                UInt32 ii1 = 0;
                RWAnimationPLG.Data[] items1 = null, items2 = null;
                try {
                    hasSubData = reader.ReadUInt32() > 0;

                    if (hasSubData) {
                        ii1 = reader.ReadUInt32();
                        UInt32 count1 = reader.ReadUInt32();
                        UInt32 count2 = reader.ReadUInt32();

                        items1 = new RWAnimationPLG.Data[count1];
                        items2 = new RWAnimationPLG.Data[count2];
                        for (UInt32 i = 0; i < count1; i++)
                            items1[i] = readAnimationPLG_Data(reader);
                        for (UInt32 i = 0; i < count2; i++)
                            items2[i] = readAnimationPLG_Data(reader);
                    }
                }
                catch (EndOfStreamException) { /* oh well... */ }
                return new RWAnimationPLG(p, boneId, hasSubData, ii1, items1, items2);
            }

            private RWAnimationPLG.Data readAnimationPLG_Data(BinaryReader reader) {
                RWAnimationPLG.Data d;
                d.name = readZString(reader);
                d.type = zzio.Utils.intToEnum<AnimSubDataType>(reader.ReadInt32());
                UInt32 count1 = reader.ReadUInt32();
                UInt32 count2 = reader.ReadUInt32();
                switch(d.type) {
                    case (AnimSubDataType.Type1): {
                            d.items1_4F = null;
                            d.items1_3F = new Vector[count1];
                            for (UInt32 i = 0; i < count1; i++)
                                d.items1_3F[i] = Vector.read(reader);
                        }break;
                    case (AnimSubDataType.Type2): {
                            d.items1_3F = null;
                            d.items1_4F = new RWAnimationPLG.SubData_4F[count1];
                            for (UInt32 i=0; i<count1; i++) {
                                d.items1_4F[i].i1 = reader.ReadSingle();
                                d.items1_4F[i].i2 = reader.ReadSingle();
                                d.items1_4F[i].i3 = reader.ReadSingle();
                                d.items1_4F[i].i4 = reader.ReadSingle();
                            }
                        }break;
                    default: {
                            throw new InvalidDataException("Unknown animation subdata item type");
                        }
                }
                d.items2 = new RWAnimationPLG.SubData2[count2];
                for (UInt32 i=0; i<count2; i++) {
                    d.items2[i].i1 = reader.ReadUInt32();
                    d.items2[i].i2 = reader.ReadUInt32();
                    d.items2[i].f = reader.ReadSingle();
                }
                return d;
            }
        }
    }
}
