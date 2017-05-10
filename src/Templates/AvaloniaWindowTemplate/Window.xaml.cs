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
            this.AttachDevTools();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
