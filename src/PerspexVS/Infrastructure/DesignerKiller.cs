using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace PerspexVS.Infrastructure
{
    internal class DesignerKiller
    {

        public static void KillAllDesigners()
        {
            var pid = Process.GetCurrentProcess().Id;
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT * " +
                    "FROM Win32_Process " +
                    "WHERE ParentProcessId=" + pid);
                ManagementObjectCollection collection = searcher.Get();
                if (collection.Count > 0)
                {
                    foreach (var item in collection)
                    {
                        UInt32 childProcessId = (UInt32) item["ProcessId"];
                        if ((int) childProcessId != Process.GetCurrentProcess().Id)
                        {
                            try
                            {
                                Process childProcess = Process.GetProcessById((int) childProcessId);
                                if (childProcess.ProcessName.Contains("Perspex.Designer"))
                                    childProcess.Kill();
                            }
                            catch
                            {
                                //
                            }
                        }
                    }
                }
            }
            catch
            {
                //
            }
        }

    }
}
