using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AvaloniaVS.Infrastructure;
using EnvDTE;
using VSLangProj;

namespace AvaloniaVS.ViewModels
{
    public class ProjectDescriptor : PropertyChangedBase
    {
        public Project Project { get; }
        public string Name { get; }
        public string TargetAssembly;

        public List<Project> References { get; set; } = new List<Project>();
        

        public ProjectDescriptor(Project project)
        {
            Project = project;
            Name = project.Name;
            TargetAssembly = Project.GetAssemblyPath();
        }

        public ProjectDescriptor(string dummyName)
        {
            Name = dummyName;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}