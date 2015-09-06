using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace PerspexVS
{
    internal sealed class PerspexBuildEvents
    {
        /// <summary>
        /// Raised when a build operation is started
        /// </summary>
        public event Action BuildBegin;
        private void OnBuildBegin() { BuildBegin.Raise(); }

        /// <summary>
        /// Reised when a build operation has ended
        /// </summary>
        public event Action BuildEnd;
        private void OnBuildEnd() { BuildEnd.Raise(); }
         

        public PerspexBuildEvents()
        {
            var dte = (DTE) Package.GetGlobalService(typeof (DTE));
            dte.Events.BuildEvents.OnBuildBegin += PdbeBuildBegin;
            dte.Events.BuildEvents.OnBuildDone += NotifyBuildEnd;
        }

        /// <summary>
        /// Global Build Done Callback
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="action"></param>
        private void NotifyBuildEnd(vsBuildScope scope, vsBuildAction action)
        {
            OnBuildEnd();
        }

        /// <summary>
        /// Global Build Start Callback
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="action"></param>
        private void PdbeBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            OnBuildBegin();
        }
    }

    public static class EventHandlerExtensions
    {
        /// <summary>
        /// Raises event with thread and null-ref safety.
        /// </summary>
        public static void Raise(this Action handler)
        {
            handler?.Invoke();
        }
    }
}
