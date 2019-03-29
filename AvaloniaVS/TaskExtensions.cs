using System.Threading.Tasks;
using Serilog;

namespace AvaloniaVS
{
    internal static class TaskExtensions
    {
        public static void FireAndForget(this Task task)
        {
            _ = task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Log.Error(t.Exception, "Exception caught by FireAndForget");
                }
            }, TaskScheduler.Default);
        }
    }
}
