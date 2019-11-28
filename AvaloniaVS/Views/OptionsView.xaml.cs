using System.Windows.Controls;
using AvaloniaVS.Services;

namespace AvaloniaVS.Views
{
    public partial class OptionsView : UserControl
    {
        public OptionsView()
        {
            InitializeComponent();
        }

        public IAvaloniaVSSettings Settings
        {
            get => DataContext as IAvaloniaVSSettings;
            set => DataContext = value;
        }
    }
}
