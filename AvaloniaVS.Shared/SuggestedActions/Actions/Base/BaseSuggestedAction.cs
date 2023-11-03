using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace AvaloniaVS.Shared.SuggestedActions.Actions.Base
{
    internal class BaseSuggestedAction
    {
        public bool HasActionSets { get; }

        public ImageMoniker IconMoniker { get; }

        public string IconAutomationText { get; }

        public string InputGestureText { get; }

        public bool HasPreview => true;

        public void Dispose()
        {
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IEnumerable<SuggestedActionSet>>(null);
        }
    }
}
