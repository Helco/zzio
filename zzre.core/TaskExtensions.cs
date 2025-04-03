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
        catch(Exception e)
        {
            if (source.Exception == null) throw new AggregateException([e]); // however that happens...
            ExceptionDispatchInfo.Throw(source.Exception);
        }
    }
}
