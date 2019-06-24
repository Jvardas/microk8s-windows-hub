using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace microk8sWinInstaller
{
    public static class Commons
    {
        public static void ExecMultipassCommand(string command, Action<string> outputCallback = null)
        {
            Process p = new Process();

            ProcessStartInfo startinfo = new ProcessStartInfo(@"C:\Program Files\Multipass\bin\multipass.exe")
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                Arguments = command,
                RedirectStandardOutput = true
            };

            p.StartInfo = startinfo;

            p.Start();

            while (!p.StandardOutput.EndOfStream)
            {
                var line = p.StandardOutput.ReadLine();
                outputCallback?.Invoke(line);
            }

            p.WaitForExit();
        }

        public static void OpenShell(string VMName)
        {
            Process p = new Process();

            ProcessStartInfo startinfo = new ProcessStartInfo(@"cmd.exe")
            {
                Arguments = $"/c multipass shell {VMName}",
            };

            p.StartInfo = startinfo;

            p.Start();
        }
    }
}
