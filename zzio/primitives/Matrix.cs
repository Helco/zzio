using System;
using System.IO;

namespace zzio.primitives
{
    public struct MatrixColumn
    {
        public float x, y, z, w;
        public Vector Vector => new Vector(x, y, z);

        public MatrixColumn(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static MatrixColumn ReadNew(BinaryReader r) => new MatrixColumn(
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle(),
            r.ReadSingle());

        public void Write(BinaryWriter writer)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(z);
            writer.Write(w);
        }
    }

    public struct Matrix
    {
        public MatrixColumn right, up, forward, pos;

        public static Matrix Identity => new Matrix(
            new MatrixColumn(1f, 0f, 0f, 0f),
            new MatrixColumn(0f, 1f, 0f, 0f),
            new MatrixColumn(0f, 0f, 1f, 0f),
            new MatrixColumn(0f, 0f, 0f, 1f));
        
        public Matrix(MatrixColumn right, MatrixColumn up, MatrixColumn forward, MatrixColumn pos)
        {
            this.right = right;
            this.up = up;
            this.forward = forward;
            this.pos = pos;
        }

        // needed as renderware sometimes saves useless flags in that last row
        public Matrix ResetRow3()
        {
            var copy = this;
            copy.right.w = copy.up.w = copy.forward.w = 0.0f;
            copy.pos.w = 1.0f;
            return copy;
        }

        public static Matrix ReadNew(BinaryReader r) => new Matrix(
            MatrixColumn.ReadNew(r),
            MatrixColumn.ReadNew(r),
            MatrixColumn.ReadNew(r),
            MatrixColumn.ReadNew(r));

        public void Write(BinaryWriter w)
        {
            right.Write(w);
            up.Write(w);
            forward.Write(w);
            pos.Write(w);
        }
    }
}
