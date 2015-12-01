using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace PerspexVS.Infrastructure
{
    internal sealed class PerspexBuildEvents
    {
        private DTEEvents _dteEvents;

        public static PerspexBuildEvents Instance { get; } = new PerspexBuildEvents();
        /// <summary>
        /// Raised when a build operation is started
        /// </summary>
        public event Action BuildBegin;

        /// <summary>
        /// Reised when a build operation has ended
        /// </summary>
        public event Action BuildEnd;

        public event Action ModeChanged;

        private PerspexBuildEvents()
        {
            var dte = (DTE) Package.GetGlobalService(typeof (DTE));
            dte.Events.BuildEvents.OnBuildBegin += PdbeBuildBegin;
            dte.Events.BuildEvents.OnBuildDone += NotifyBuildEnd;
            _dteEvents = dte.Events.DTEEvents;
            _dteEvents.ModeChanged += NotifyModeChanged;
        }

        private void NotifyModeChanged(vsIDEMode lastmode)
        {
            DesignerKiller.KillAllDesigners();
            ModeChanged?.Invoke();
        }

        /// <summary>
        /// Global Build Done Callback
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="action"></param>
        private void NotifyBuildEnd(vsBuildScope scope, vsBuildAction action)
        {
            if (action < vsBuildAction.vsBuildActionBuild || action > vsBuildAction.vsBuildActionRebuildAll)
            {
                //Not an actual build event, we are here if user hits Start and there is nothing to build
                DesignerKiller.KillAllDesigners();
                return;
            }
            BuildEnd?.Invoke();
        }

        /// <summary>
        /// Global Build Start Callback
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="action"></param>
        private void PdbeBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            BuildBegin?.Invoke();
            DesignerKiller.KillAllDesigners();
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
