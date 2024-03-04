using Serilog;

namespace zzre;

public static class LoggerExtensions
{
    // Let's not log the entire namespace chain, the class name should suffice
    public static ILogger For<T>(this ILogger parent) =>
        parent.ForContext("SourceContext", typeof(T).Name);

    public static ILogger For(this ILogger parent, string name) =>
        parent.ForContext("SourceContext", name);

    public static ILogger GetLoggerFor<T>(this ITagContainer diContainer) =>
        diContainer.GetTag<ILogger>().For<T>();
}
