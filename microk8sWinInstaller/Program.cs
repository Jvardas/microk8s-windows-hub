using Octokit;
using ShellProgressBar;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Threading;

namespace microk8sWinInstaller
{
    class Program
    {
        static void Main(string[] args)
        {

            Console.WriteLine("Searching latest multipass release...");

            var gitClient = new GitHubClient(new ProductHeaderValue("MultipassInstaller"));
            var releases = gitClient.Repository.Release.GetAll("CanonicalLtd", "multipass").GetAwaiter().GetResult();
            var latestRelease = releases.Where(r => r.Assets.Any(a => a.ContentType == "application/x-msdos-program")).OrderByDescending(r => r.PublishedAt).FirstOrDefault();

            if (latestRelease == null)
            {
                return;
            }

            var asset = latestRelease.Assets.First(a => a.ContentType == "application/x-msdos-program");

            var assetUrl = asset.BrowserDownloadUrl;

            DownloadInstaller(assetUrl, asset.Name);

            if (!Directory.Exists(@"C:\Program Files\Multipass"))
            {
                DeployApplication(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), asset.Name));
            }

            var cloudConfigPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "cloud-config.yaml");
            
            var launchCommand = $"launch --cloud-init \"{cloudConfigPath}\"";
            
            ExecMultipassCommand(launchCommand);


            ExecMultipassCommand("ls");

            Console.ReadKey();

        }

        public static void DownloadInstaller(string uri, string targetName)
        {
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

        private static void ExecMultipassCommand(string command)
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
                Console.WriteLine(line);
            }

            p.WaitForExit();
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
