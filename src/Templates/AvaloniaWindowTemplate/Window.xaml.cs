using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace $rootnamespace$
{
    public class $safeitemrootname$ : Window
    {
        public $safeitemrootname$()
        {
            this.InitializeComponent();
            App.AttachDevTools(this);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
