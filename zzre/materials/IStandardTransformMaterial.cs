using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using zzre.rendering;

namespace zzre.materials
{
    public interface IStandardTransformMaterial
    {
        UniformBinding<Matrix4x4> Projection { get; }
        UniformBinding<Matrix4x4> View { get; }
        UniformBinding<Matrix4x4> World { get; }
    }

    public static class IStandardTransformMaterialExtensions
    {
        public static void LinkTransformsTo (this IStandardTransformMaterial me, IStandardTransformMaterial other)
        {
            me.Projection.Buffer = other.Projection.Buffer;
            me.View.Buffer = other.View.Buffer;
            me.World.Buffer = other.World.Buffer;
        }

        public static void LinkTransformsTo(this IStandardTransformMaterial me, UniformBuffer<Matrix4x4> projection, UniformBuffer<Matrix4x4> view, UniformBuffer<Matrix4x4> world)
        {
            me.Projection.Buffer = projection.Buffer;
            me.View.Buffer = view.Buffer;
            me.World.Buffer = world.Buffer;
        }
    }
}
