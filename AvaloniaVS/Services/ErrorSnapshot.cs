using System.Collections.Generic;
using Avalonia.Remote.Protocol.Designer;
using Microsoft.VisualStudio.Shell.TableManager;

namespace AvaloniaVS.Services
{
    internal class ErrorSnapshot : TableEntriesSnapshotBase
    {
        private readonly IList<ExceptionDetails> _errors;
        private readonly int _versionNumber;

        public override int Count => _errors.Count;
        public override int VersionNumber => _versionNumber;
    }
}
