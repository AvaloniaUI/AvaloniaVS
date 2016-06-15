using System.Collections.Generic;
using System.Linq;
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

        void ScanReferences(Project proj)
        {
            var vsproj = proj.Object as VSProject;
            if(vsproj == null)
                return;
            foreach(Reference r in vsproj.References)
            {
                try
                {
                    if (r.SourceProject == null || References.Contains(r.SourceProject))
                        continue;
                    References.Add(r.SourceProject);
                    ScanReferences(r.SourceProject);
                }
                catch
                {
                    
                }
            }
        }

        public bool ChangedFrom(ProjectDescriptor other)
        {
            return other.TargetAssembly != TargetAssembly || other.Project != Project || other.Name != Name ||
                   !other.References.SequenceEqual(References);
        }

        public ProjectDescriptor(Project project)
        {
            Project = project;
            ScanReferences(project);
            TargetAssembly = Project.GetAssemblyPath();
            Name = Project.Name;
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