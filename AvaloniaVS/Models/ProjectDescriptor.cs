using System.Collections.Generic;
using AvaloniaVS.Services;
using EnvDTE;

namespace AvaloniaVS.Models
{
    public class ProjectDescriptor
    {
        public Project Project { get; }
        public string Name { get; }
        public string TargetAssembly;
        public Dictionary<string, string> RunnableOutputs = new Dictionary<string, string>();

        public List<Project> ProjectReferences { get; set; } = new List<Project>();
        public List<string> References { get; set; } = new List<string>();
        
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

                    if (o.IsNetCore)
                    {
                        if (o.TargetAssembly.ToLowerInvariant().EndsWith(".exe") ||
                            o.OutputTypeIsExecutable)
                            RunnableOutputs[o.TargetFramework] = o.TargetAssembly;
                    }
                }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
