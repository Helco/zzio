using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using zzre.core.rendering;
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

        public static void LinkTransformsTo(this IStandardTransformMaterial me, UniformBuffer<Matrix4x4>? projection = null, UniformBuffer<Matrix4x4>? view = null, UniformBuffer<Matrix4x4>? world = null)
        {
            if (projection != null)
                me.Projection.Buffer = projection.Buffer;
            if (view != null)
                me.View.Buffer = view.Buffer;
            if (world != null)
                me.World.Buffer = world.Buffer;
        }

        public static void LinkTransformsTo(this IStandardTransformMaterial me, Camera camera)
        {
            me.Projection.BufferRange = camera.ProjectionRange;
            me.View.BufferRange = camera.ViewRange;
        }
    }
}
