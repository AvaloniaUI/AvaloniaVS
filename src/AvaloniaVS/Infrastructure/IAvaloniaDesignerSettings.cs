namespace AvaloniaVS.Infrastructure
{
    public interface IAvaloniaDesignerSettings
    {
        void Save();
        bool IsDesignerEnabled { get; set; }
        SplitOrientation SplitOrientation { get; set; }
        DocumentView DocumentView { get; set; }
        bool IsReversed { get; set; }
    }
}