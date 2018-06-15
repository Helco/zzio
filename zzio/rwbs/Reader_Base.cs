using System;
using System.IO;
using System.Collections.Generic;

namespace zzio {
    namespace rwbs {
        public partial class Reader {
            private Byte[] buffer;

            public Reader(Byte[] buffer) {
                this.buffer = buffer;
            }

            private BaseSection readSection(ListSection parent, int start, int size, out int sectionSize) {
                if (size < 12)
                    throw new EndOfStreamException();

                MemoryStream stream = new MemoryStream(buffer, (int)start, (int)size, false);
                BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8);

                //read header
                UInt32 sectionIdInt = reader.ReadUInt32();
                sectionSize = (Int32)reader.ReadUInt32();
                UInt32 rwVersion = reader.ReadUInt32();
                stream = new MemoryStream(buffer, (int)(start + 12), (int)sectionSize, false);

                SectionId sectionId;
                if (!Enum.IsDefined(typeof(SectionId), (Int32)sectionIdInt))
                    sectionId = SectionId.Unknown;
                else
                    sectionId = (SectionId)sectionIdInt;

                if (Utils.isSectionListSection(sectionId)) {
                    //read childs
                    ListSection curSection = null;
                    List<BaseSection> childs = new List<BaseSection>();
                    BaseSection child;
                    int childStart = start + 12, remSize = sectionSize, childSize;

                    //the only list section that does not have a first struct section
                    if (sectionId == SectionId.Extension)
                        curSection = new RWExtension(parent);

                    while (remSize > 12) {
                        child = readSection(curSection == null ? parent : curSection, childStart, remSize, out childSize);

                        childSize += 12; //add header
                        childStart += childSize;
                        remSize -= childSize;

                        if (Utils.usesFirstStruct(sectionId) && curSection == null) {
                            if (child.sectionId != SectionId.Struct)
                                throw new InvalidDataException("First section of \"" + sectionId.ToString() + "\" has to be a struct");
                            RWStruct str = (RWStruct)child;
                            switch (sectionId) {
                                case (SectionId.Texture): { curSection = readTexture(parent, str); } break;
                                case (SectionId.Material): { curSection = readMaterial(parent, str); } break;
                                case (SectionId.MaterialList): { curSection = readMaterialList(parent, str); } break;
                                case (SectionId.AtomicSection): { curSection = readAtomicSection(parent, str); } break;
                                case (SectionId.PlaneSection): { curSection = readPlaneSection(parent, str); } break;
                                case (SectionId.World): { curSection = readWorld(parent, str); } break;
                                case (SectionId.FrameList): { curSection = readFrameList(parent, str); }break;
                                case (SectionId.Geometry): { curSection = readGeometry(parent, str); }break;
                                case (SectionId.Clump): { curSection = readClump(parent, str); }break;
                                case (SectionId.Atomic): { curSection = readAtomic(parent, str); }break;
                                case (SectionId.GeometryList): { curSection = readGeometryList(parent, str); }break;
                                default: { curSection = new UnknownListSection(sectionId, parent); childs.Add(child); } break;
                            }
                        }
                        else
                            childs.Add(child);

                    }
                    curSection.childs = childs.ToArray();
                    return curSection;
                }
                else {
                    MemoryStream subStream = new MemoryStream(buffer, (int)(start + 12), (int)sectionSize);
                    BinaryReader subReader = new BinaryReader(subStream, System.Text.Encoding.UTF8);
                    switch (sectionId) {
                        case (SectionId.Struct): {
                                byte[] subBuffer = new byte[sectionSize];
                                Array.Copy(buffer, start + 12, subBuffer, 0, sectionSize);
                                return new RWStruct(parent, subBuffer);
                            }
                        case (SectionId.String): { return readString(parent, subStream, sectionSize); }
                        case (SectionId.MorphPLG): { return readMorphPLG(parent, subReader); }
                        case (SectionId.SkinPLG): { return readSkinPLG(parent, subReader); }
                        case (SectionId.BinMeshPLG): { return readBinMeshPLG(parent, subReader); }
                        case (SectionId.AnimationPLG): { return readAnimationPLG(parent, subReader); }
                        default: {
                                byte[] subBuffer = new byte[sectionSize];
                                Array.Copy(buffer, start + 12, subBuffer, 0, sectionSize);
                                return new UnknownSection(sectionIdInt, parent, subBuffer);
                            }
                    }
                }
            }

            public BaseSection readSection() {
                int dummy;
                return readSection(null, 0, buffer.Length, out dummy);
            }
        }
    }
}