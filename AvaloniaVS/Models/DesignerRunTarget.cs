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
        /// Gets or sets the full path to the target assembly.
        /// </summary>
        public string TargetAssembly { get; set; }
    }
}
