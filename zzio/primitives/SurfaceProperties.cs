using System;
using System.IO;

namespace zzio.scn
{
    [Serializable]
    public struct SurfaceProperties : IEquatable<SurfaceProperties>
    {
        public float ambient;
        public float specular;
        public float diffuse;

        public SurfaceProperties(float ambient, float specular, float diffuse) =>
            (this.ambient, this.specular, this.diffuse) = (ambient, specular, diffuse);

        public static SurfaceProperties ReadNew(BinaryReader reader) => new SurfaceProperties(
            reader.ReadSingle(),
            reader.ReadSingle(),
            reader.ReadSingle());

        public void Write(BinaryWriter writer)
        {
            writer.Write(ambient);
            writer.Write(specular);
            writer.Write(diffuse);
        }

        public override string ToString() => $"{{Ambient: {ambient}, Specular: {specular}, Diffuse: {diffuse}}}";

        public bool Equals(SurfaceProperties other) =>
            ambient == other.ambient &&
            specular == other.specular &&
            diffuse == other.diffuse;
        public override bool Equals(object? obj) => obj is SurfaceProperties properties && Equals(properties);
        public override int GetHashCode() => HashCode.Combine(ambient, specular, diffuse);
        public static bool operator ==(SurfaceProperties left, SurfaceProperties right) => left.Equals(right);
        public static bool operator !=(SurfaceProperties left, SurfaceProperties right) => !(left == right);
    }
}