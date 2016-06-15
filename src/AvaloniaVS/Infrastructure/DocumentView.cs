using System.ComponentModel;

namespace AvaloniaVS.Infrastructure
{
    public enum DocumentView
    {
        [Description("Split View")]
        SplitView,

        [Description("Source View")]
        SourceView,

        [Description("Design View")]
        DesignView
    }
}