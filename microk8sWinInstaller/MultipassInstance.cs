using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static microk8sWinInstaller.Commons;

namespace microk8sWinInstaller
{
    public class MultipassInstance
    {
        public MultipassInstance(string name, string status) : this(name, (MultipassInstanceStatus)Enum.Parse(typeof(MultipassInstanceStatus), status))
        {
        }

        public MultipassInstance(string name, MultipassInstanceStatus status)
        {
            this.InstanceName = name;
            this.MultipassInstanceStatus = status;

            switch (MultipassInstanceStatus)
            {
                case MultipassInstanceStatus.None:
                case MultipassInstanceStatus.Starting:
                    throw new InvalidOperationException(this.InstanceName + ": Invalid instance status");
                case MultipassInstanceStatus.Stopped:
                    InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                    {
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

                    InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                    {
                        Description = $"Start {this.InstanceName} and open shell",
                        Command = new Func<string>(() =>
                        {
                            var cmdOutput = "";
                            Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                            ExecMultipassCommand("start " + this.InstanceName, output =>
                            {
                                cmdOutput += output;
                                Console.WriteLine(output);
                            });

                            OpenShell(this.InstanceName);

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

                    InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                    {
                        Description = $"Open shell for {this.InstanceName}",
                        Command = new Func<string>(() =>
                        {
                            Console.WriteLine($"Executing command. Please wait for the subprocess to return...");
                            OpenShell(this.InstanceName);
                            return $"Shell openned for {this.InstanceName}";
                        })
                    });
                    break;
                case MultipassInstanceStatus.Deleted:
                    break;
            }

            //TODO microk8s /snap commands

            if (MultipassInstanceStatus == MultipassInstanceStatus.Deleted)
            {
                InstanceCommands.Add(InstanceCommands.Count, new MultipassInstanceCommand(this)
                {
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


        }


        public string InstanceName { get; set; }
        public MultipassInstanceStatus MultipassInstanceStatus { get; set; }

        public Dictionary<int, MultipassInstanceCommand> InstanceCommands = new Dictionary<int, MultipassInstanceCommand>();

    }

    public class MultipassInstanceCommand
    {
        public MultipassInstanceCommand(MultipassInstance instance)
        {
            this.Instance = instance;
        }

        public MultipassInstance Instance { get; private set; }
        public string Description { get; set; }
        public Func<string> Command { get; set; }

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
