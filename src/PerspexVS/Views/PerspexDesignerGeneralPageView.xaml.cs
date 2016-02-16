using System.ComponentModel.Composition;
using PerspexVS.ViewModels;

namespace PerspexVS.Views
{
    [Export]
    public partial class PerspexDesignerGeneralPageView
    {
        public PerspexDesignerGeneralPageView()
        {
            InitializeComponent();
        }

        [Import(typeof(PerspexDesignerGeneralPageViewModel))]
        public new object DataContext
        {
            get { return base.DataContext; }
            set { base.DataContext = value; }
        }
    }
}
