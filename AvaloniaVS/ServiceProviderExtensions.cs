using System;
using Microsoft;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS
{
    internal static class ServiceProviderExtensions
    {
        public static T GetService<T>(this IServiceProvider sp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = (T)sp.GetService(typeof(T));
            Assumes.Present(result);
            return result;
        }

        public static TResult GetService<TResult, TService>(this IServiceProvider sp)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = sp.GetService(typeof(TService));
            Assumes.Present(result);
            return (TResult)result;
        }

        public static T GetMefService<T>(this IServiceProvider sp) where T : class
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var componentModel = sp.GetService<IComponentModel, SComponentModel>();
            var result = componentModel.GetService<T>();
            Assumes.Present(result);
            return result;
        }
    }
}
