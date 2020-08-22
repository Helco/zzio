using System;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace zzre
{
    // unfortunately very much copy-paste from Rect, CSharp has no better generics support

    public struct Bounds
    {
        public Vector3 Center;
        public Vector3 Size;

        public Vector3 HalfSize
        {
            get => Size / 2;
            set => Size = value * 2;
        }

        public Vector3 Min
        {
            get => Center - HalfSize;
            set
            {
                var sizeDelta = Min - value;
                Center -= sizeDelta / 2;
                Size += sizeDelta;
            }
        }

        public Vector3 Max
        {
            get => Center + HalfSize;
            set
            {
                var sizeDelta = value - Max;
                Center += sizeDelta / 2;
                Size += sizeDelta;
            }
        }

        public static Bounds Zero => new Bounds();

        public Bounds(float x, float y, float z, float w, float h, float d)
        {
            Center = new Vector3(x, y, z);
            Size = new Vector3(w, h, d);
        }

        public Bounds(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = size;
        }

        public static Bounds FromMinMax(Vector3 min, Vector3 max) => new Bounds((min + max) / 2, (max - min));
        public static Bounds Union(params Bounds[] bounds) => bounds.Skip(1).Aggregate(bounds.First(), (prev, next) => prev.Union(next));

        public Bounds OffsettedBy(float x, float y, float z) => new Bounds(Center + new Vector3(x, y, z), Size);
        public Bounds OffsettedBy(Vector3 off) => new Bounds(Center + off, Size);
        public Bounds GrownBy(float x, float y, float z) => new Bounds(Center, Size + new Vector3(x, y, z));
        public Bounds GrownBy(Vector3 off) => new Bounds(Center, Size + off);
        public Bounds ScaledBy(float s) => new Bounds(Center, Size * s);
        public Bounds ScaledBy(float x, float y, float z) => new Bounds(Center, Vector3.Multiply(Size, new Vector3(x, y, z)));
        public Bounds ScaledBy(Vector3 s) => new Bounds(Center, Vector3.Multiply(Size, s));
        public Bounds At(Vector3 newCenter) => new Bounds(newCenter, Size);
        public Bounds WithSizeOf(Vector3 newSize) => new Bounds(Center, newSize);
        public Bounds Union(Bounds other) => Bounds.FromMinMax(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

        public bool IsInside(Vector3 pos) =>
            pos.X >= Min.X && pos.X < Max.X &&
            pos.Y >= Min.Y && pos.Y < Max.Y &&
            pos.Z >= Min.Y && pos.Z < Max.Z;
    }
}
