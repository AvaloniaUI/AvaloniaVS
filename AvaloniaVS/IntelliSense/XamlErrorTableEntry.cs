using System;
using Avalonia.Remote.Protocol.Designer;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlErrorTableEntry : TableEntryBase
    {
        private readonly ExceptionDetails _error;

        public XamlErrorTableEntry(ExceptionDetails error)
        {
            _error = error;
        }

        public override bool TryGetValue(string keyName, out object content)
        {
            switch (keyName)
            {
                case StandardTableKeyNames.Column:
                    content = _error.LinePosition - 1;
                    return true;
                case StandardTableKeyNames.ErrorSeverity:
                    content = __VSERRORCATEGORY.EC_ERROR;
                    return true;
                case StandardTableKeyNames.Line:
                    content = _error.LineNumber - 1;
                    return true;
                case StandardTableKeyNames.Text:
                    content = _error.Message;
                    return true;
                default:
                    content = null;
                    return false;
            }
        }
    }
}
