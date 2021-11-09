using Avalonia.Remote.Protocol.Designer;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableManager;

namespace AvaloniaVS.IntelliSense
{
    internal class XamlErrorTableEntry : TableEntryBase
    {
        private readonly string _projectName;
        private readonly string _fileName;
        private readonly ExceptionDetails _error;

        public XamlErrorTableEntry(
            string projectName,
            string path,
            ExceptionDetails error)
        {
            _projectName = projectName;
            _fileName = path;
            _error = error;
        }

        public override bool TryGetValue(string keyName, out object content)
        {
            switch (keyName)
            {
                case StandardTableKeyNames.Column:
                    content = (_error.LinePosition ?? 1) - 1;
                    return _error.LinePosition.HasValue;
                case StandardTableKeyNames.ErrorSeverity:
                    content = __VSERRORCATEGORY.EC_ERROR;
                    return true;
                case StandardTableKeyNames.DocumentName:
                    content = _fileName;
                    return true;
                case StandardTableKeyNames.Line:
                    content = (_error.LineNumber ?? 1) - 1;
                    return _error.LineNumber.HasValue;
                case StandardTableKeyNames.ProjectName:
                    content = _projectName;
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
