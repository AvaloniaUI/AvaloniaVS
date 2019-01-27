using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;

namespace AvaloniaVS.Views
{
    public class DesignerPane : WindowPane
    {
        TextBlock _content;

        public DesignerPane()
        {
            _content = new TextBlock { Text = "Hello World!" };
        }

        public override object Content => _content;

        protected override void Initialize()
        {
            base.Initialize();
        }
    }
}
