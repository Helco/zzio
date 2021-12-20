using System.Numerics;

namespace zzre
{
    public struct Rect
    {
        public Vector2 Center;
        public Vector2 Size;

        public Vector2 HalfSize
        {
            get => Size / 2;
            set => Size = value * 2;
        }

        public Vector2 Min
        {
            get => Center - HalfSize;
            set
            {
                var sizeDelta = Min - value;
                Center -= sizeDelta / 2;
                Size += sizeDelta;
            }
        }

        public Vector2 Max
        {
            get => Center + HalfSize;
            set
            {
                var sizeDelta = value - Max;
                Center += sizeDelta / 2;
                Size += sizeDelta;
            }
        }

        public static Rect Zero => new Rect();

        public Rect(float x, float y, float w, float h)
        {
            Center = new Vector2(x, y);
            Size = new Vector2(w, h);
        }

        public Rect(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = size;
        }

        public static Rect FromMinMax(Vector2 min, Vector2 max) => new Rect((min + max) / 2f, max - min);
        public static Rect FromTopLeftSize(Vector2 min, Vector2 size) => new Rect(min + size / 2, size);

        public Rect OffsettedBy(float x, float y) => new Rect(Center + new Vector2(x, y), Size);
        public Rect OffsettedBy(Vector2 off) => new Rect(Center + off, Size);
        public Rect GrownBy(float x, float y) => new Rect(Center, Size + new Vector2(x, y));
        public Rect GrownBy(Vector2 off) => new Rect(Center, Size + off);
        public Rect ScaledBy(float s) => new Rect(Center, Size * s);
        public Rect ScaledBy(float x, float y) => new Rect(Center, Vector2.Multiply(Size, new Vector2(x, y)));
        public Rect ScaledBy(Vector2 s) => new Rect(Center, Vector2.Multiply(Size, s));
        public Rect At(Vector2 newCenter) => new Rect(newCenter, Size);
        public Rect WithSizeOf(Vector2 newSize) => new Rect(Center, newSize);

        public bool IsInside(Vector2 pos) => pos.X >= Min.X && pos.X < Max.X && pos.Y >= Min.Y && pos.Y < Max.Y;

        /// <summary>
        /// Calculates the relative position of a point to the rectangle
        /// </summary>
        /// <returns>A vector whose components are clamped to 0..1 when inside of the rectangle</returns>
        public Vector2 RelativePos(Vector2 pos) => (pos - Min) / Size;

        /// <summary>
        /// Calculates the absolute position of a point relative of the rectangle
        /// </summary>
        /// <returns>A position inside the rectangle if the parameter was clamped to 0..1</returns>
        public Vector2 AbsolutePos(Vector2 pos) => Min + pos * Size;
    }
}
