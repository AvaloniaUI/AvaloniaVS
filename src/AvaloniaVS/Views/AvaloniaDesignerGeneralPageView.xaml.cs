using System.ComponentModel.Composition;
using AvaloniaVS.ViewModels;

namespace AvaloniaVS.Views
{
    [Export]
    public partial class AvaloniaDesignerGeneralPageView
    {
        public AvaloniaDesignerGeneralPageView()
        {
            InitializeComponent();
        }

        [Import(typeof(AvaloniaDesignerGeneralPageViewModel))]
        public new object DataContext
        {
            get { return base.DataContext; }
            set { base.DataContext = value; }
        }
    }
}
