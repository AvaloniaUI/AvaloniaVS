using AvaloniaVS.Utils;

namespace AvaloniaVS.Models
{
    /// <summary>
    /// Holds information about a <see cref="ProjectInfo"/>'s outputs.
    /// </summary>
    public class ProjectOutputInfo
    {
        /// <summary>
        /// Gets the full path to the target assembly for the output.
        /// </summary>
        public string TargetAssembly { get; }

        /// <summary>
        /// Gets the friendly name of framework for the output.
        /// </summary>
        public string TargetFramework { get; }

        /// <summary>
        /// Gets the long name of framework for the output.
        /// </summary>
        public string TargetFrameworkIdentifier { get; }

        /// <summary>
        /// Gets the RuntimeIdentifier of the project.
        /// </summary>
        public string RuntimeIdentifier { get; }

        public string TargetPlatformIdentifier { get; }

        /// <summary>
        /// Gets the full path to the Avalonia.Designer.HostApp.dll to use.
        /// </summary>
        public string HostApp { get; }

        /// <summary>
        /// Gets a value indicating whether the target framework is .NET Framework.
        /// </summary>
        public bool IsNetFramework => FrameworkInfoUtils.IsNetFramework(TargetFrameworkIdentifier);

        /// <summary>
        /// Gets a value indicating whether the target framework is .NET Core.
        /// </summary>
        public bool IsNetCore => FrameworkInfoUtils.IsNetCoreApp(TargetFrameworkIdentifier);

        /// <summary>
        /// Gets a value indicating whether the target framework is .NET Standard.
        /// </summary>
        public bool IsNetStandard => FrameworkInfoUtils.IsNetStandard(TargetFrameworkIdentifier);

        public ProjectOutputInfo(
            string targetAssembly, string targetFramework, string targetFrameworkIdentifier, string hostApp, string runtimeIdentifier, string targetPlatformIdentifier)
        {
            TargetAssembly = targetAssembly;
            TargetFramework = targetFramework;
            TargetFrameworkIdentifier = targetFrameworkIdentifier;
            HostApp = hostApp;
            RuntimeIdentifier = runtimeIdentifier;
            TargetPlatformIdentifier = targetPlatformIdentifier;
        }
    }
}
