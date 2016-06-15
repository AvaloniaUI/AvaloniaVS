using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvaloniaVS.IntelliSense;

namespace Sandbox
{
    static class Program
    {
        public static void Main()
        {
            MetadataLoader.LoadMetadata(@"..\..\..\..\Avalonia\samples\XamlTestApplication\bin\Debug\XamlTestApplication.exe");

        }
    }
}
