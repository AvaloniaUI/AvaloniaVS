using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PerspexVS.IntelliSense;

namespace Sandbox
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            
            InitializeComponent();
            Xml.SelectionChanged += Xml_SelectionChanged;
            Xml.Text = @"<Root><wa";

        }

        private void Xml_SelectionChanged(object sender, RoutedEventArgs _)
        {

            try
            {
                Results.Text = XmlParser.Parse(Xml.Text.Substring(0, Xml.SelectionStart)).ToString();
            }
            catch (Exception e)
            {
                Results.Text = e.ToString();
            }
        }
    }
}
