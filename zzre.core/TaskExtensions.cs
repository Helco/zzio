using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace zzre;

public static class TaskExtensions
{
    public static void WaitAndRethrow(this Task task)
    {
        try
        {
            task.Wait();
        }
        catch(AggregateException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    // from https://github.com/dotnet/runtime/issues/47605
    public static async Task WithAggregateException(this Task source)
    {
        try
        {
            await source.ConfigureAwait(false);
        }
        catch
        {
            if (source.Exception == null) throw;
            ExceptionDispatchInfo.Capture(source.Exception).Throw();
        }
    }
}
