using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace $rootnamespace$
{
    public class $safeitemrootname$ : Window
    {
        public $safeitemrootname$()
        {
            this.InitializeComponent();
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
