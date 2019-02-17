namespace AvaloniaVS.Models
{
    /// <summary>
    /// Holds information about a <see cref="ProjectInfo"/>'s outputs.
    /// </summary>
    public class ProjectOutputInfo
    {
        /// <summary>
        /// Gets or sets the full path to the target assembly for the output.
        /// </summary>
        public string TargetAssembly { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the output type represents an executable.
        /// </summary>
        public bool OutputTypeIsExecutable { get; set; }

        /// <summary>
        /// Gets or sets the target framework for the output.
        /// </summary>
        public string TargetFramework { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the target framework is the dotnet framework.
        /// </summary>
        public bool IsFullDotNet { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the target framework is dotnet core.
        /// </summary>
        public bool IsNetCore { get; set; }
    }
}
