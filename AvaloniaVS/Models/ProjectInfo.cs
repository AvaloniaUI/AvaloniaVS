﻿using System.Collections.Generic;
using EnvDTE;

namespace AvaloniaVS.Models
{
    /// <summary>
    /// Holds information required by the designer about a project.
    /// </summary>
    internal class ProjectInfo
    {
        /// <summary>
        /// Gets or sets a value indicating whether the project is an executable.
        /// </summary>
        public bool IsExecutable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the project is an library.
        /// From MSDN (https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build):
        /// To produce a library, omit the <OutputType> property or change its value to Library. 
        /// The IL DLL for a library doesn't contain entry points and can't be executed.
        /// </summary>
        public bool IsLibrary { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the project is a startup project.
        /// </summary>
        public bool IsStartupProject { get; set; }

        /// <summary>
        /// Gets or sets the project name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the underlying EnvDTE project.
        /// </summary>
        public Project Project { get; set; }

        /// <summary>
        /// Gets or sets the project's outputs.
        /// </summary>
        public IReadOnlyList<ProjectOutputInfo> Outputs { get; set; }

        /// <summary>
        /// Gets or sets the project's project references.
        /// </summary>
        public IReadOnlyList<Project> ProjectReferences { get; set; }

        /// <summary>
        /// Gets or sets the project's assembly references.
        /// </summary>
        public IReadOnlyList<string> References { get; set; }
    }
}
