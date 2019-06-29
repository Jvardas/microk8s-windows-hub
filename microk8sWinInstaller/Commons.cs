using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace microk8sWinInstaller
{
    public static class Commons
    {
        public static void ExecMultipassCommand(string command, Action<string> outputCallback = null, bool redirectOutput = true)
        {
            Process p = new Process();

            ProcessStartInfo startinfo = new ProcessStartInfo(@"multipass")
            {
                CreateNoWindow = !redirectOutput,
                UseShellExecute = !redirectOutput,
                Arguments = command,
                RedirectStandardOutput = redirectOutput,
            };

            p.StartInfo = startinfo;

            p.Start();

            if (redirectOutput)
            {
                while (!p.StandardOutput.EndOfStream)
                {
                    var line = p.StandardOutput.ReadLine();
                    outputCallback?.Invoke(line);
                }

                p.WaitForExit();
            }
        }

        public static void StartMultipassInstance(string instanceName)
        {
            var cmdOutput = "";
            Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
            ExecMultipassCommand("start " + instanceName, output =>
            {
                cmdOutput += output;
                Console.WriteLine(output);
            });
        }

        public static int ExecCommandThroughSSH(string ipv4, string command, out string output, out string error)
        {
            var pk = new PrivateKeyFile(Path.Combine(@"C:\Windows\SysNative\config\systemprofile\AppData\Roaming\multipassd\ssh-keys", "id_rsa"));
            var ci = new ConnectionInfo(ipv4, "multipass", new PrivateKeyAuthenticationMethod("multipass", pk));

            using (var client = new SshClient(ci))
            {
                client.Connect();
                var cmdResult = client.RunCommand(command);
                output = cmdResult.Result;
                error = cmdResult.Error;
                return cmdResult.ExitStatus;
            }
        }
    }
}
