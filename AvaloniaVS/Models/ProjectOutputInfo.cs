using System.Text.RegularExpressions;

namespace AvaloniaVS.Models
{
    /// <summary>
    /// Holds information about a <see cref="ProjectInfo"/>'s outputs.
    /// </summary>
    public class ProjectOutputInfo
    {
        private static readonly Regex s_desktopFrameworkRegex = new Regex("^net[0-9]+$");

        /// <summary>
        /// Gets or sets the full path to the target assembly for the output.
        /// </summary>
        public string TargetAssembly { get; set; }

        /// <summary>
        /// Gets or sets the target framework for the output.
        /// </summary>
        public string TargetFramework { get; set; }

        /// <summary>
        /// Gets the full path to the Avalonia.Designer.HostApp.dll to use.
        /// </summary>
        public string HostApp { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the target framework is the dotnet framework.
        /// </summary>
        public bool IsFullDotNet => TargetFramework != null ?
            s_desktopFrameworkRegex.IsMatch(TargetFramework) : false;

        /// <summary>
        /// Gets or sets a value indicating whether the target framework is dotnet core.
        /// </summary>
        public bool IsNetCore => TargetFramework?.StartsWith("netcoreapp") ?? false;

        /// <summary>
        /// Gets or sets a value indicating whether the target framework is netstandard.
        /// </summary>
        public bool IsNetStandard => TargetFramework?.StartsWith("netstandard") ?? false;
    }
}
