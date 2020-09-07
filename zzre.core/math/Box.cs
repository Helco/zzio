using System;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace zzre
{
    // unfortunately very much copy-paste from Rect, CSharp has no better generics support

    public struct Box
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

        public static Box Zero => new Box();

        public Box(float x, float y, float z, float w, float h, float d)
        {
            Center = new Vector3(x, y, z);
            Size = new Vector3(w, h, d);
        }

        public Box(Vector3 center, Vector3 size)
        {
            Center = center;
            Size = size;
        }

        public static Box FromMinMax(Vector3 min, Vector3 max) => new Box((min + max) / 2, (max - min));
        public static Box Union(params Box[] bounds) => bounds.Skip(1).Aggregate(bounds.First(), (prev, next) => prev.Union(next));

        public Box OffsettedBy(float x, float y, float z) => new Box(Center + new Vector3(x, y, z), Size);
        public Box OffsettedBy(Vector3 off) => new Box(Center + off, Size);
        public Box GrownBy(float x, float y, float z) => new Box(Center, Size + new Vector3(x, y, z));
        public Box GrownBy(Vector3 off) => new Box(Center, Size + off);
        public Box ScaledBy(float s) => new Box(Center, Size * s);
        public Box ScaledBy(float x, float y, float z) => new Box(Center, Vector3.Multiply(Size, new Vector3(x, y, z)));
        public Box ScaledBy(Vector3 s) => new Box(Center, Vector3.Multiply(Size, s));
        public Box At(Vector3 newCenter) => new Box(newCenter, Size);
        public Box WithSizeOf(Vector3 newSize) => new Box(Center, newSize);
        public Box Union(Box other) => FromMinMax(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
        public Box Union(Vector3 v) => Union(new Box(v, Vector3.Zero));

        public bool IsInside(Vector3 pos) =>
            pos.X >= Min.X && pos.X < Max.X &&
            pos.Y >= Min.Y && pos.Y < Max.Y &&
            pos.Z >= Min.Y && pos.Z < Max.Z;
    }
}
