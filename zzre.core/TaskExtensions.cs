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
}
