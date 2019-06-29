using Octokit;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using static microk8sWinInstaller.Commons;

[assembly: AssemblyInformationalVersionAttribute("Octokit Reflection Error when ILMerged")] // <-- Bypass the error described in the string on the left
namespace microk8sWinInstaller
{
    class Program
    {
        /*TODO*/
        //delete all
        //stop all
        //generate batches
        /*-------------------*/


        public enum ProgramState
        {
            Main,
            Commands,
            Exit
        }

        static ProgramState programState = ProgramState.Main;
        static MultipassInstance selectedInstance = null;

        static void Main(string[] args)
        {
            while (true)
            {
                switch (programState)
                {
                    case ProgramState.Main:
                        MainScreen();
                        break;
                    case ProgramState.Commands:
                        CommandsScreen();
                        break;
                    case ProgramState.Exit:
                        return;
                }
            }
        }

        public static void MainScreen()
        {
            Console.Clear();
            selectedInstance = null;

            ServiceController multipassService = ServiceController.GetServices().FirstOrDefault(sc => sc.ServiceName == "Multipass");

            if (multipassService == null)
            {
                CreateNewInstance();
                multipassService = ServiceController.GetServices().FirstOrDefault(sc => sc.ServiceName == "Multipass");
            }

            switch (multipassService.Status)
            {
                case ServiceControllerStatus.ContinuePending:
                case ServiceControllerStatus.StartPending:
                    Console.WriteLine("Multipassd is going to be running soon");
                    multipassService.WaitForStatus(ServiceControllerStatus.Running);
                    break;
                case ServiceControllerStatus.PausePending:
                    Console.WriteLine("Multipassd is being paused");
                    multipassService.WaitForStatus(ServiceControllerStatus.Paused);
                    Console.WriteLine("Multipassd is continuing");
                    multipassService.Continue();
                    multipassService.WaitForStatus(ServiceControllerStatus.Running);
                    break;
                case ServiceControllerStatus.StopPending:
                    Console.WriteLine("Multipassd is being stopped");
                    multipassService.WaitForStatus(ServiceControllerStatus.Stopped);
                    Console.WriteLine("Multipassd is starting");
                    multipassService.Start();
                    multipassService.WaitForStatus(ServiceControllerStatus.Running);
                    break;
                case ServiceControllerStatus.Paused:
                    Console.WriteLine("Multipassd is continuing");
                    multipassService.Continue();
                    multipassService.WaitForStatus(ServiceControllerStatus.Running);
                    break;
                case ServiceControllerStatus.Stopped:
                    Console.WriteLine("Multipassd is starting");
                    multipassService.Start();
                    multipassService.WaitForStatus(ServiceControllerStatus.Running);
                    break;
                default:
                    break;
            }

            Console.WriteLine("Multipassd is running");

            var instanceCount = 0;
            var activeInstances = new Dictionary<int, MultipassInstance>();
            ExecMultipassCommand("list", line =>
            {
                Console.Write((instanceCount == 0 ? "#" : (instanceCount - 1).ToString()) + " ");
                Console.WriteLine($"{line}");
                if (instanceCount++ == 0) return;

                var matches = Regex.Matches(line, @"(.+?)\s+");
                var name = matches[0].Groups[1]?.Value;
                var status = matches[1].Groups[1]?.Value;
                var ipv4 = matches[2].Groups[1]?.Value;
                if (!String.IsNullOrWhiteSpace(name))
                {
                    activeInstances.Add(instanceCount - 2, new MultipassInstance(name, status, ipv4)); // -2 cause the first line has the table headers and no vm names
                }
            });

            if (!activeInstances.Any())
            {
                var newInstance = CreateNewInstance();
                activeInstances.Add(0, newInstance);
            }

            Console.WriteLine();
            var selectedInstanceId = -1;
            if (activeInstances.Count >= 1)
            {
                while (true)
                {
                    Console.WriteLine($"Enter a vm id (0 - {activeInstances.Count - 1}) to proceed, type \"new\" to create a new instance, p to purge all deleted instances or type \"exit\" to exit: ");
                    var strSelectedInstanceId = Console.ReadLine();
                    if (strSelectedInstanceId == "p")
                    {

                        ExecMultipassCommand("purge", output =>
                        {
                            Console.WriteLine(output);
                        });
                        return;
                    }
                    else if (strSelectedInstanceId == "exit")
                    {
                        programState = ProgramState.Exit;
                        return;
                    }
                    else if (strSelectedInstanceId == "new")
                    {
                        var newInstance = CreateNewInstance();
                        selectedInstanceId = activeInstances.Count;
                        activeInstances.Add(selectedInstanceId, newInstance);
                        break;
                    }
                    if (Int32.TryParse(strSelectedInstanceId, out selectedInstanceId) && 0 <= selectedInstanceId && activeInstances.Count >= selectedInstanceId)
                    {
                        break;
                    }
                }
            }
            else
            {
                selectedInstanceId = 0;
            }

            var menuItems = new Dictionary<int, string>();

            selectedInstance = activeInstances[selectedInstanceId];
            var isPopulated = selectedInstance.PopulateCommands();
            if (isPopulated)
            {
                programState = ProgramState.Commands;
            }
            else
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        public static void CommandsScreen()
        {
            Console.Clear();
            Console.WriteLine("Available commands:");
            foreach (var cmd in selectedInstance.InstanceCommands)
            {
                Console.WriteLine($"{cmd.Key} {cmd.Value.Description}");
            }

            Console.WriteLine();

            while (true)
            {
                var selectedCommandId = -1;
                while (true)
                {
                    Console.WriteLine($"Enter a command id (0 - {selectedInstance.InstanceCommands.Count - 1}) to proceed or type \"exit\" to go back:");
                    var strSelectedCommandId = Console.ReadLine();
                    if (strSelectedCommandId == "exit")
                    {
                        programState = ProgramState.Main;
                        return;
                    }
                    if (Int32.TryParse(strSelectedCommandId, out selectedCommandId) && 0 <= selectedCommandId && selectedInstance.InstanceCommands.Count > selectedCommandId)
                    {
                        break;
                    }
                }

                var selectedCommand = selectedInstance.InstanceCommands[selectedCommandId];
                Console.WriteLine();
                var commandResult = selectedCommand.Command();
                Console.WriteLine(commandResult);
                Console.WriteLine();
                if (selectedCommand.ShouldExitAfterExecution)
                {
                    programState = ProgramState.Main;
                    return;
                }
            }
        }

        public static MultipassInstance CreateNewInstance(bool openShellOnComplete = false)
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var startupShortcutPath = Path.Combine(startupFolder, "microk8sWinInstaller.lnk");
            var executablePath = Assembly.GetEntryAssembly().Location;


            Console.WriteLine("Searching latest multipass release...");

            var gitClient = new GitHubClient(new ProductHeaderValue("MultipassInstaller"));
            var releases = gitClient.Repository.Release.GetAll("CanonicalLtd", "multipass").GetAwaiter().GetResult();
            var latestRelease = releases.Where(r => r.Assets.Any(a => a.ContentType == "application/x-msdos-program")).OrderByDescending(r => r.PublishedAt).FirstOrDefault();

            if (latestRelease == null)
            {
                return null;
            }

            var asset = latestRelease.Assets.First(a => a.ContentType == "application/x-msdos-program");

            var assetUrl = asset.BrowserDownloadUrl;

            if (!Directory.Exists(@"C:\Program Files\Multipass"))
            {
                DownloadInstaller(assetUrl, Path.Combine(Path.GetDirectoryName(executablePath), asset.Name));
                CreateShortcut(startupShortcutPath, executablePath);
                DeployApplication(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), asset.Name));
            }

            if (File.Exists(startupShortcutPath))
            {
                File.Delete(startupShortcutPath);
            }

            var cloudConfigPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "cloud-config.yaml");

            var launchCommand = $"launch --cloud-init \"{cloudConfigPath}\"";

            string vmName = "";
            string status = "";
            string ipv4 = "";
            ExecMultipassCommand(launchCommand, line =>
            {
                var matches = Regex.Matches(line, @"(.+?)\s+");
                if (matches.Count < 3)
                {
                    return;
                }
                vmName = matches[0].Groups[1]?.Value;
                status = matches[1].Groups[1]?.Value;
                ipv4 = matches[2].Groups[1]?.Value;
            });

            if (!String.IsNullOrWhiteSpace(vmName) && openShellOnComplete)
            {
                ExecMultipassCommand("shell " + vmName, redirectOutput: false);
            }

            return new MultipassInstance(vmName, MultipassInstanceStatus.Running, ipv4);
        }

        public static void DownloadInstaller(string uri, string targetName)
        {
            if (File.Exists(targetName))
                return;

            var mre = new ManualResetEvent(false);

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            IAsyncResult asyncResult = null;

            const int totalTicks = 100;
            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };

            var pbar = new ProgressBar(totalTicks, $"Downloading {targetName}", options);

            asyncResult = request.BeginGetResponse((state) =>
            {
                var response = request.EndGetResponse(asyncResult) as HttpWebResponse;
                var length = response.ContentLength;

                var responseStream = response.GetResponseStream();
                var file = GetContentWithProgressReporting(responseStream, length, pbar);

                File.WriteAllBytes(targetName, file);

                mre.Set();
            }, null);

            mre.WaitOne();

            pbar.Dispose();
        }

        private static string initialMessage = "";
        private static void UpdateProgressBar(int v, ProgressBar pb, string message)
        {
            if (pb == null)
            {
                return;
            }

            if (v == 0 && string.IsNullOrWhiteSpace(initialMessage))
            {
                initialMessage = pb.Message;
            }

            pb.Tick(v, $"{initialMessage} {message}");
        }

        private static byte[] GetContentWithProgressReporting(Stream responseStream, long contentLength, ProgressBar pb)
        {
            UpdateProgressBar(0, pb, $"0/{contentLength / (1024f * 1024f):0.##} MB");

            // Allocate space for the content
            var data = new byte[contentLength];
            int currentIndex = 0;
            int bytesReceived = 0;
            var buffer = new byte[256];
            do
            {
                bytesReceived = responseStream.Read(buffer, 0, 256);
                Array.Copy(buffer, 0, data, currentIndex, bytesReceived);
                currentIndex += bytesReceived;

                // Report percentage
                double percentage = (double)currentIndex / contentLength;
                UpdateProgressBar((int)(percentage * 100), pb, $"{currentIndex / (1024f * 1024f):0.##}/{contentLength / (1024f * 1024f):0.##} MB");
            } while (currentIndex < contentLength);

            UpdateProgressBar(100, pb, $"{contentLength / (1024f * 1024f):0.##}/{contentLength / (1024f * 1024f):0.##} MB");
            return data;
        }

        public static void CreateShortcut(string shortcutPath, string targetPath)
        {
            Type t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")); //Windows Script Host Shell Object
            dynamic shell = Activator.CreateInstance(t);
            try
            {
                var lnk = shell.CreateShortcut(shortcutPath);
                try
                {
                    lnk.TargetPath = targetPath;
                    lnk.IconLocation = "shell32.dll, 1";
                    lnk.Save();
                }
                finally
                {
                    Marshal.FinalReleaseComObject(lnk);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        public static void DeployApplication(string executableFilePath)
        {
            PowerShell powerShell = null;
            Console.WriteLine(" ");
            Console.WriteLine("Deploying application...");
            try
            {
                using (powerShell = PowerShell.Create())
                {
                    powerShell.AddScript($"$setup=Start-Process '{executableFilePath}' -ArgumentList '/S' -Wait -PassThru");

                    Collection<PSObject> PSOutput = powerShell.Invoke();
                    foreach (PSObject outputItem in PSOutput)
                    {
                        if (outputItem != null)
                        {

                            Console.WriteLine(outputItem.BaseObject.GetType().FullName);
                            Console.WriteLine(outputItem.BaseObject.ToString() + "\n");
                        }
                    }

                    if (powerShell.Streams.Error.Count > 0)
                    {
                        string temp = powerShell.Streams.Error.First().ToString();
                        Console.WriteLine("Error: {0}", temp);

                    }
                    else
                    {
                        Console.WriteLine("Installation has completed successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occured: {0}", ex.InnerException);
            }
            finally
            {
                if (powerShell != null)
                    powerShell.Dispose();
            }
        }
    }
}
