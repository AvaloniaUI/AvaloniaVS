using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace $rootnamespace$
{
    public partial class $safeitemrootname$ : Window
    {
        public $safeitemrootname$()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
