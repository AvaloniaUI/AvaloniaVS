using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    Debug.WriteLine($"FireAndForget exception: {t.Exception}.");
                }
            }, TaskScheduler.Default);
        }
    }
}
