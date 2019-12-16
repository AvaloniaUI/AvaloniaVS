using AvaloniaVS.Views;

namespace AvaloniaVS.Models
{
    /// <summary>
    /// Represents an executable target for the <see cref="AvaloniaDesigner"/> previewer.
    /// </summary>
    internal class DesignerRunTarget
    {
        /// <summary>
        /// Gets or sets the target's name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the full path to the executable assembly.
        /// </summary>
        public string ExecutableAssembly { get; set; }

        /// <summary>
        /// Gets or sets the full path to the assembly containing the XAML.
        /// </summary>
        public string XamlAssembly { get; set; }

        /// <summary>
        /// Gets the full path to the Avalonia.Designer.HostApp.dll to use.
        /// </summary>
        public string HostApp { get; set; }
    }
}
