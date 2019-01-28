namespace AvaloniaVS.Models
{
    public class ProjectOutputInfo
    {
        public string TargetAssembly { get; set; }
        public bool OutputTypeIsExecutable { get; set; }
        public string TargetFramework { get; set; }
        public bool IsFullDotNet { get; set; }
        public bool IsNetCore { get; set; }
    }
}
