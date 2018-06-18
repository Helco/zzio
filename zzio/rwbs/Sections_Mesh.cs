using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using zzio.primitives;

namespace zzio {
    namespace rwbs {
        [System.Serializable]
        public class RWFrameList : ListSection {
            public Frame[] frames;

            public RWFrameList(ListSection p, Frame[] f) : base(SectionId.FrameList, p) {
                frames = f;
            }
        }

        [System.Serializable]
        public class RWGeometry : ListSection {
            [System.Serializable]
            public struct MorphTarget {
                public Vector bsphereCenter;
                public float bsphereRadius;
                public Vector[] vertices, normals;
            }
            public GeometryFormat format;
            public float ambient, specular, diffuse;
            public UInt32[] colors;
            public TexCoord[,] texCoords;
            public Triangle[] triangles;
            public MorphTarget[] morphTargets;

            public RWGeometry(ListSection p, GeometryFormat f, float a, float s, float d, UInt32[] c, TexCoord[,] tc, Triangle[] t,
                MorphTarget[] m) : base(SectionId.Geometry, p) {
                format = f;
                ambient = a;
                specular = s;
                diffuse = d;
                colors = c;
                texCoords = tc;
                triangles = t;
                morphTargets = m;
            }
        }

        [System.Serializable]
        public class RWClump : ListSection {
            //not really representive?
            public UInt32 atomicCount, lightCount, camCount;

            public RWClump(ListSection p, UInt32 a, UInt32 l, UInt32 c) : base(SectionId.Clump, p) {
                atomicCount = a;
                lightCount = l;
                camCount = c;
            }
        }

        [System.Serializable]
        public class RWAtomic : ListSection {
            public UInt32 frameIndex, geometryIndex;
            public AtomicFlags flags;
            public UInt32 unused;

            public RWAtomic(ListSection p, UInt32 fi, UInt32 g, AtomicFlags fl, UInt32 u) : base(SectionId.Atomic, p) {
                frameIndex = fi;
                geometryIndex = g;
                flags = fl;
                unused = u;
            }
        }

        [System.Serializable]
        public class RWGeometryList : ListSection {
            public UInt32 geometryCount; //not really representive?

            public RWGeometryList(ListSection p, UInt32 c) : base(SectionId.GeometryList, p) {
                geometryCount = c;
            }
        }

        [System.Serializable]
        public class RWMorphPLG : BaseSection {
            public UInt32 morphTargetIndex; //I don't know it either

            public RWMorphPLG(ListSection p, UInt32 m) : base(SectionId.MorphPLG, p) {
                morphTargetIndex = m;
            }
        }

        [System.Serializable]
        public class RWSkinPLG : BaseSection {
            //source: https://github.com/kabbi/zanzarah-tools/blob/master/dff-parser.coffee
            //the format used is incompatible to the one described by www.gtamodding.com
            //maybe I should investigate in this further, but it seems to work for Zanzarah...

            [System.Serializable]
            public struct Bone {
                public UInt32 id, idx;
                public BoneFlags flags;
                public Vector right, up, at, pos; //does kabbi here just don't like 4 dimensional matrices?
                public UInt32 p1, p2, p3, p4; 
            }
            public byte[,] vertexIndices; //4 per vertex
            public float[,] vertexWeights; //^^
            public Bone[] bones;

            public RWSkinPLG(ListSection p, byte[,] vi, float[,] vw, Bone[] b) : base(SectionId.SkinPLG, p) {
                vertexIndices = vi;
                vertexWeights = vw;
                bones = b;
            }
        }

        [System.Serializable]
        public class RWBinMeshPLG : BaseSection {
            [System.Serializable]
            public struct SubMesh {
                public UInt32 matIndex;
                public UInt32[] indices; //as ZanZarah is DirectX exclusive this should be 32 bit 
            }

            public BinMeshFlags flags;
            public UInt32 totalIndexCount; //this could also be calculated
            public SubMesh[] subMeshes;

            public RWBinMeshPLG(ListSection p, BinMeshFlags f, UInt32 tic, SubMesh[] sm) : base(SectionId.BinMeshPLG, p) {
                flags = f;
                totalIndexCount = tic;
                subMeshes = sm;
            }
        }

        [System.Serializable]
        public class RWAnimationPLG : BaseSection {
            //source: https://github.com/kabbi/zanzarah-tools/blob/master/dff-parser.coffee
            //kabbi was very uncertain about this apparently, as he called this "strangeAnimData"

            [System.Serializable]
            public struct SubData_4F {
                public float i1, i2, i3, i4;
            }
            [System.Serializable]
            public struct SubData2 {
                public UInt32 i1, i2;
                public float f;
            }
            [System.Serializable]
            public struct Data {
                public string name;
                public AnimSubDataType type;
                public Vector[] items1_3F;
                public SubData_4F[] items1_4F;
                public SubData2[] items2;
            }

            public Int32 boneId;
            public bool hasSubData;
            public UInt32 ii1; //this is subdata already
            Data[] items1, items2;

            public RWAnimationPLG(ListSection p, Int32 boneId, bool hs, UInt32 ii1, Data[] it1, Data[] it2) : base(SectionId.AnimationPLG, p) {
                this.boneId = boneId;
                hasSubData = hs;
                this.ii1 = ii1;
                items1 = it1;
                items2 = it2;
            }
        }
    }
}