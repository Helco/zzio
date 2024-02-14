namespace zzre;
using System;
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

    public static OptionalReference<T> TryGet<T>(this DefaultEcs.Entity e) => e.IsAlive && e.Has<T>()
        ? new(ref e.Get<T>())
        : default;

    public static bool TryGet<T>(this DefaultEcs.Entity e, out T result)
    {
        result = default!;
        if (e.IsAlive && e.Has<T>())
        {
            result = e.Get<T>();
            return true;
        }
        return false;
    }
}
