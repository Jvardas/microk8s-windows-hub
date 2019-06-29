using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static microk8sWinInstaller.Commons;


namespace microk8sWinInstaller
{
    public class MultipassInstance
    {
        public string InstanceName { get; set; }
        public MultipassInstanceStatus MultipassInstanceStatus { get; set; }
        public string IPv4 { get; set; }

        public Dictionary<int, MultipassInstanceCommand> InstanceCommands = new Dictionary<int, MultipassInstanceCommand>();

        public MultipassInstance(string name, string status, string ipv4) : this(name, (MultipassInstanceStatus)Enum.Parse(typeof(MultipassInstanceStatus), status), ipv4)
        {
        }

        public MultipassInstance(string name, MultipassInstanceStatus status, string ipv4)
        {
            this.InstanceName = name;
            this.MultipassInstanceStatus = status;
            this.IPv4 = ipv4;
        }

        public bool PopulateCommands()
        {
            InstanceCommands.Clear();
            if (MultipassInstanceStatus == MultipassInstanceStatus.Deleted)
            {
                InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                {
                    RequireRunningInsance = false,
                    Description = $"Recover {this.InstanceName}",
                    Command = new Func<string>(() =>
                    {
                        Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                        ExecMultipassCommand("recover " + this.InstanceName, output =>
                        {
                            Console.WriteLine(output);
                        });
                        return $"{this.InstanceName} recovered";
                    })
                });
            }
            else
            {
                InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                {
                    Description = $"Open shell for {this.InstanceName}",
                    Command = new Func<string>(() =>
                    {
                        Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                        ExecMultipassCommand("shell " + this.InstanceName, redirectOutput: false);
                        return $"Shell openned for {this.InstanceName}";
                    })
                });

                switch (MultipassInstanceStatus)
                {
                    case MultipassInstanceStatus.None:
                    case MultipassInstanceStatus.Starting:
                        //throw new InvalidOperationException(this.InstanceName + ": Invalid instance status");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(this.InstanceName + ": Invalid instance status");
                        Console.ResetColor();
                        return false;
                    case MultipassInstanceStatus.Stopped:
                        InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                        {
                            RequireRunningInsance = false,
                            Description = $"Start {this.InstanceName}",
                            Command = new Func<string>(() =>
                            {
                                var cmdOutput = "";
                                Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                                ExecMultipassCommand("start " + this.InstanceName, output =>
                                {
                                    cmdOutput += output;
                                    Console.WriteLine(output);
                                });

                                return cmdOutput;
                            })
                        });
                        break;
                    case MultipassInstanceStatus.Running:
                        InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                        {
                            Description = $"Stop {this.InstanceName}",
                            Command = new Func<string>(() =>
                            {
                                var cmdOutput = "";
                                Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                                ExecMultipassCommand("stop " + this.InstanceName, output =>
                                {
                                    cmdOutput += output;
                                    Console.WriteLine(output);
                                });
                                return cmdOutput;
                            })
                        });
                        break;
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(new CamelCaseNamingConvention())
                    .WithNamingConvention(new HyphenatedNamingConvention())
                    .Build();
                var config = deserializer.Deserialize<Config>(File.ReadAllText("config.yaml"));
                var snapCommmands = config.Snaps.SelectMany(s => s.Commands);
                foreach (var snapCommand in snapCommmands)
                {
                    InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                    {
                        Description = snapCommand.Key,
                        Command = new Func<string>(() =>
                        {
                            Console.WriteLine($"Executing command. Please wait for the subprocess to return...");

                            string output, error;
                            var exitCode = ExecCommandThroughSSH(this.IPv4, snapCommand.Value, out output, out error);
                            if (exitCode == 0)
                            {
                                Console.WriteLine(output);
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.Error.WriteLine(output);
                                Console.ResetColor();
                            }
                            return $"";
                        })
                    });
                }

                InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                {
                    ShouldExitAfterExecution = true,
                    RequireRunningInsance = false,
                    Description = $"Delete {this.InstanceName}",
                    Command = new Func<string>(() =>
                    {
                        Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                        ExecMultipassCommand("delete " + this.InstanceName, output =>
                        {
                            Console.WriteLine(output);
                        });
                        return $"{this.InstanceName} deleted";
                    })
                });
            }

            return true;
        }

    }

    public class MultipassInstanceCommand
    {
        public MultipassInstanceCommand(MultipassInstance instance)
        {
            this.Instance = instance;
        }

        public bool RequireRunningInsance { get; set; } = true;
        public bool ShouldExitAfterExecution { get; set; } = false;
        public MultipassInstance Instance { get; private set; }
        public string Description { get; set; }

        private Func<string> innerCommand;
        private Func<string> command;
        public Func<string> Command {
            get {
                return innerCommand;
            }
            set {
                command = value;
                innerCommand = new Func<string>(() =>
                {
                    if (RequireRunningInsance && Instance.MultipassInstanceStatus != MultipassInstanceStatus.Running)
                    {
                        StartMultipassInstance(Instance.InstanceName);
                    }

                    ExecMultipassCommand("list", line =>
                    {
                        var matches = Regex.Matches(line, @"(.+?)\s+");
                        var vmName = matches[0].Groups[1]?.Value;
                        var status = matches[1].Groups[1]?.Value;
                        var ipv4 = matches[2].Groups[1]?.Value;
                        if (vmName == Instance.InstanceName)
                        {
                            Instance.IPv4 = ipv4;
                            Instance.MultipassInstanceStatus = (MultipassInstanceStatus)Enum.Parse(typeof(MultipassInstanceStatus), status);
                        }
                    });

                    var cmd = command();
                    return cmd;
                });
            }
        }

    }

    public enum MultipassInstanceStatus
    {
        None,
        Stopped,
        Running,
        Starting,
        Deleted
    }
}
