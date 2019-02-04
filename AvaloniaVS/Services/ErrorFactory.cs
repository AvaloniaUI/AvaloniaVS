using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.TableManager;

namespace AvaloniaVS.Services
{
    internal class ErrorFactory : TableEntriesSnapshotFactoryBase
    {
        private readonly PreviewerProcess _process;
        private ErrorSnapshot _snapshot;

        public ErrorFactory(PreviewerProcess process)
        {
            _process = process;
        }

        public override ITableEntriesSnapshot GetCurrentSnapshot() => _snapshot;

        public override ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            return (versionNumber == _snapshot.VersionNumber) ? _snapshot : null;
        }
    }
}
