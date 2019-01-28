using System;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS
{
    internal static class ServiceProviderExtensions
    {
        public static T GetService<T>(this IServiceProvider sp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return (T)sp.GetService(typeof(T));
        }

        public static TResult GetService<TResult, TService>(this IServiceProvider sp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return (TResult)sp.GetService(typeof(TService));
        }

    }
}
