namespace PerspexVS.Infrastructure
{
    public interface IPerspexDesignerSettings
    {
        void Save();
        bool IsDesignerEnabled { get; set; }
        SplitOrientation SplitOrientation { get; set; }
        DocumentView DocumentView { get; set; }
        bool IsReversed { get; set; }
    }
}