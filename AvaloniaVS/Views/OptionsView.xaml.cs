using System;
using System.Linq;
using System.Windows.Controls;
using AvaloniaVS.Services;
using Serilog.Events;

namespace AvaloniaVS.Views
{
    /// <summary>
    /// Interaction logic for OptionsView.xaml
    /// </summary>
    public partial class OptionsView : UserControl
    {
        public OptionsView()
        {
            InitializeComponent();
        }

        public static DesignerViewType[] DesignerViewTypes { get; } = Enum.GetValues(typeof(DesignerViewType)).Cast<DesignerViewType>().ToArray();
        public static LogEventLevel[] LogEventLevels { get; } = Enum.GetValues(typeof(LogEventLevel)).Cast<LogEventLevel>().ToArray();

        public IAvaloniaVSSettings Settings
        {
            get => DataContext as IAvaloniaVSSettings;
            set => DataContext = value;
        }
    }
}
