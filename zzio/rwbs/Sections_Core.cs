using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace zzio {
    namespace rwbs {
        public abstract class BaseSection {
            [JsonProperty("type", Order = -2)]
            public SectionId sectionId;
            [JsonIgnore]
            public ListSection parent;

            public BaseSection(SectionId id, ListSection p) {
                sectionId = id;
                parent = p;
            }

            public ListSection getParentById(SectionId id) {
                ListSection p = parent;
                while (p != null && p.sectionId != id)
                    p = p.parent;
                return p;
            }
        }

        public abstract class ListSection : BaseSection {
            [JsonProperty("childs", Order = 2)]
            public BaseSection[] childs;

            public ListSection(SectionId id, ListSection p, BaseSection[] sects = null) : base(id, p) {
                childs = sects;
            }

            public BaseSection[] getChildsById(SectionId id) {
                List<BaseSection> list = new List<BaseSection>();
                foreach (BaseSection s in childs) {
                    if (s.sectionId == id)
                        list.Add(s);
                }
                return list.ToArray();
            }

            public BaseSection getFirstChildById(SectionId id) {
                BaseSection[] sections = getChildsById(id);
                return sections.Length == 0 ? null : sections[0];
            }
        }

        [System.Serializable]
        public class UnknownSection : BaseSection {
            public UInt32 rawId;
            public Byte[] data;

            public UnknownSection(UInt32 id, ListSection p, Byte[] data) : base(SectionId.Unknown, p) {
                //Console.WriteLine("unknown section: " + id);
                this.rawId = id;
                this.data = data;
            }
        }

        //this is only until all known list sections are correctly parsed
        [System.Serializable]
        public class UnknownListSection : ListSection {
            public UnknownListSection(SectionId id, ListSection p, BaseSection[] childs = null) : base(id, p, childs) {
                //Console.WriteLine("unknown list section: " + id.ToString());
            }
        }

        [System.Serializable]
        public class RWStruct : BaseSection {
            public Byte[] data;

            public RWStruct(ListSection p, Byte[] data) : base(SectionId.Struct, p) {
                this.data = data;
            }

            public System.IO.BinaryReader getBinaryReader() {
                return new System.IO.BinaryReader(new System.IO.MemoryStream(data, false),
                    System.Text.Encoding.UTF8);
            }
        }

        [System.Serializable]
        public class RWString : BaseSection {
            public string value;
            public RWString(ListSection p, string value) : base(SectionId.String, p) {
                this.value = value;
            }
        }

        [System.Serializable]
        public class RWExtension : ListSection {
            public RWExtension(ListSection p, BaseSection[] s = null) : base(SectionId.Extension, p, s) { }
        }

        [System.Serializable]
        public class RWTexture : ListSection {
            public TextureFilterMode filterMode;
            public TextureAddressingMode uAddressingMode;
            public TextureAddressingMode vAddressingMode;
            public bool doesUseMipLevels;

            public String name {
                get {
                    BaseSection[] stringSections = this.getChildsById(SectionId.String);
                    if (stringSections.Length > 0)
                        return ((RWString)stringSections[0]).value;
                    return null;
                }
            }

            public String alphaLayerName {
                get {
                    BaseSection[] stringSections = this.getChildsById(SectionId.String);
                    if (stringSections.Length > 1)
                        return ((RWString)stringSections[1]).value;
                    return null;
                }
            }

            public RWTexture(ListSection p, TextureFilterMode fm, TextureAddressingMode uam, TextureAddressingMode vam,
                bool useMip, BaseSection[] childs = null) : base(SectionId.Texture, p, childs) {
                filterMode = fm;
                uAddressingMode = uam;
                vAddressingMode = vam;
                doesUseMipLevels = useMip;
            }
        }

        [System.Serializable]
        public class RWMaterial : ListSection {
            public UInt32 flags;
            public UInt32 unused;
            [JsonConverter(typeof(JsonHexNumberConverter))]
            public UInt32 color;
            public bool isTextured;
            public float ambient;
            public float specular;
            public float diffuse;

            public RWMaterial(ListSection p, UInt32 f, UInt32 c, UInt32 u, bool isT, float a, float s, float d,
                BaseSection[] childs = null) : base(SectionId.Material, p, childs) {
                flags = f;
                unused = u;
                color = c;
                isTextured = isT;
                ambient = a;
                specular = s;
                diffuse = d;
            }
        }

        [System.Serializable]
        public class RWMaterialList : ListSection {
            public Int32[] matIndices;

            public RWMaterialList(ListSection p, Int32[] i, BaseSection[] childs = null) : base(SectionId.MaterialList, p, childs) {
                matIndices = i;
            }
        }

        [System.Serializable]
        public class RWAtomicSection : ListSection {
            public UInt32 matIdBase;
            public Vector bbox1, bbox2;
            public UInt32 unused1, unused2;
            public Vector[] vertices;
            public Normal[] normals;
            public UInt32[] colors;
            public TexCoord[] texCoords1, texCoords2;
            public Triangle[] triangles;

            public RWAtomicSection(ListSection p, UInt32 m, Vector bb1, Vector bb2, UInt32 u1, UInt32 u2, Vector[] v, Normal[] n,
                UInt32[] c, TexCoord[] tc1, TexCoord[] tc2, Triangle[] tr, BaseSection[] childs = null)
                : base(SectionId.AtomicSection, p, childs) {
                matIdBase = m;
                bbox1 = bb1;
                bbox2 = bb2;
                unused1 = u1;
                unused2 = u2;
                vertices = v;
                normals = n;
                colors = c;
                texCoords1 = tc1;
                texCoords2 = tc2;
                triangles = tr;
            }
        }

        [System.Serializable]
        public class RWPlaneSection : ListSection {
            public UInt32 sectorType;
            public float value, leftValue, rightValue;
            public bool leftIsWorldSector, rightIsWorldSector;

            public RWPlaneSection(ListSection p, UInt32 t, float v, bool lws, bool rws, float lv, float rv, BaseSection[] childs = null)
                : base(SectionId.PlaneSection, p, childs) {
                sectorType = t;
                value = v;
                leftValue = lv;
                rightValue = rv;
                leftIsWorldSector = lws;
                rightIsWorldSector = rws;
            }
        }

        [System.Serializable]
        public class RWWorld : ListSection {
            public bool rootIsWorldSector;
            public Vector origin;
            public float ambient, specular, diffuse;
            public UInt32 numTriangles, numVertices, numPlaneSectors, numWorldSectors, colSectorSize;
            public GeometryFormat format;

            public RWWorld(ListSection p, bool riws, Vector o, float a, float s, float d, UInt32 nt, UInt32 nv, UInt32 nps,
                UInt32 nws, UInt32 css, GeometryFormat f, BaseSection[] childs = null) : base(SectionId.World, p, childs) {
                rootIsWorldSector = riws;
                origin = o;
                ambient = a;
                specular = s;
                diffuse = d;
                numTriangles = nt;
                numVertices = nv;
                numPlaneSectors = nps;
                numWorldSectors = nws;
                colSectorSize = css;
                format = f;
            }
        }
    }
}