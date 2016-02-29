using Perspex.Controls;
using Perspex.Markup.Xaml;

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
            PerspexXamlLoader.Load(this);
        }
    }
}
