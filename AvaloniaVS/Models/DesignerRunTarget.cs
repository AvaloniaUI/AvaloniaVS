using System;

namespace AvaloniaVS.Models
{
    internal class DesignerRunTarget : IEquatable<DesignerRunTarget>, IComparable<DesignerRunTarget>
    {
        public string Name { get; set; }
        public string TargetAssembly { get; set; }
        public bool IsContainingProject { get; set; }

        public int CompareTo(DesignerRunTarget other) => IsContainingProject ? -1 : Name.CompareTo(other.Name);
        public override bool Equals(object obj) => Equals(obj as DesignerRunTarget);
        public bool Equals(DesignerRunTarget other) => Name == other?.Name && TargetAssembly == other?.TargetAssembly;

        public override int GetHashCode()
        {
            var hash = 27;
            hash = (13 * hash) + Name.GetHashCode();
            hash = (13 * hash) + TargetAssembly.GetHashCode();
            return hash;
        }
    }
}
