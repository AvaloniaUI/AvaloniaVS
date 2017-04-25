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

        void ScanReferences(Project proj, Dictionary<string, Project> dic)
        {
            var vsproj = proj.Object as VSProject;
            if(vsproj == null)
                return;
            var dir = Path.GetDirectoryName(proj.FullName);
            foreach(Reference r in vsproj.References)
            {

                try
                {
                    Project src;
                    var ospecp = r.GetType()
                        .GetProperty("OriginalItemSpec",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (ospecp == null)
                        //SLOW
                        src = r.SourceProject;
                    else
                    {
                        var ospec = ospecp.GetMethod.Invoke(r, null) as string;
                        if(ospec == null)
                            continue;
                        var path = Path.GetFullPath(Path.Combine(dir, ospec)).ToLowerInvariant();
                        if (!dic.TryGetValue(path, out src))
                            continue;

                    }
                    if (src == null || References.Contains(src))
                        continue;
                    References.Add(src);
                    ScanReferences(src, dic);
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

        public ProjectDescriptor(Project project, Dictionary<string, Project> dic)
        {
            Project = project;
            Name = project.Name;
            ScanReferences(project, dic);
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