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
        public Dictionary<string, string> RunnableOutputs = new Dictionary<string, string>();

        public List<Project> References { get; set; } = new List<Project>();
        
        public ProjectDescriptor(Project project)
        {
            Project = project;
            Name = project.Name;
            var nfo = Project.GetProjectOutputInfo();
            if(nfo!=null)
                foreach (var o in nfo)
                {
                    if (TargetAssembly == null)
                        TargetAssembly = o.TargetAssembly;

                    if (o.IsFullDotNet || o.IsNetCore)
                    {
                        if (o.TargetAssembly.ToLowerInvariant().EndsWith(".exe") ||
                            o.OutputType?.ToLowerInvariant() == "exe")
                            RunnableOutputs[o.TargetFramework] = o.TargetAssembly;
                    }
                }
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