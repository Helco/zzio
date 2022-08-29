namespace zzre.game;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class DefaultECSExtensions
{
    public static void DisposeAll(this DefaultEcs.EntityQueryBuilder builder)
    {
        Array.ForEach(
            builder.AsEnumerable().ToArray(),
            DisposeEntity);
    }

    public static void DisposeAll(this DefaultEcs.EntityQueryBuilder.EitherBuilder builder)
    {
        Array.ForEach(
            builder.AsEnumerable().ToArray(),
            DisposeEntity);
    }

    public static void DisposeAll(this DefaultEcs.EntitySet set)
    {
        Array.ForEach(
            set.GetEntities().ToArray(),
            DisposeEntity);
    }

    private static void DisposeEntity(DefaultEcs.Entity e) => e.Dispose();
}
