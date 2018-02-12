using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace AvaloniaVS.Infrastructure
{
    internal class DesignerKiller
    {
        static List<Process> s_designers = new List<Process>();


        public static void KillAllDesigners()
        {
            lock (s_designers)
            {
                foreach (var p in s_designers.ToList())
                {
                    try
                    {
                        p.Kill();
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                s_designers.Clear();
            }
        }

        public static void Register(Process proc)
        {
            lock (s_designers)
                s_designers.Add(proc);
        }
    }
}
