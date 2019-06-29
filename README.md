# microk8s-windows-hub

Simple windows installer, in the form of a console application that uses multipass to install microk8s.

# Prerequisites
|||
|------------------|------------|
| Operating System | Windows 10 |
| Hypervisor       | Hyper-V    |

# Installation process

The app automatically downloads the latest multipass release for windows from [github] and installs it silently on the background.

After the installation has completed, your system will restart automatically so that Hyper-V gets enabled. **Don't panic it's normal**
After rebooting, the installer automatically continues with the creation and launch of a VM with a random name using the following command: `launch --cloud-init cloud-config.yaml`. Since multipass supports `cloud-init`, microk8s is installed in the spawned VM with the help of the cloud configuration file `cloud-config.yaml`. 

# MWH Usage
After the instalation phase is over you can navigate through the menu and open instances, manage them or directly issue microk8s commands from the cli menu. 

You are then greeted from the main menu, which is actually the output of multipass command `multipass list`. After selecting your instance of choice byt typing the number that is asociated with the said instance, you will be prompted by the commands menu. There 
you can select from a number of choices, which range from simple multipass commands, eg.
`multipass start <instance name>,
multipass delete <instance name>`
but also all of the microk8s commands. The output of the microk8s commands is shown and you can easily continue issuing commands. 
MWH uses a .yaml file, config.yaml, that includes microk8s commands, which means that if you choose so you can use this file to execute other or more snap commands, since the menu system is dynamic.

After some investigation I found out that an [issue] posted in multipass's repo, is that you cannot get the output of the `multipass exec <instance name> -- [command]` - which is the common way multipass is executing commands outside of instances - and redirect it in the cli. What I did to overcome this was to execute commands through a custom ssh connection to the selected instance so the user can then see the output

[github]: https://github.com/CanonicalLtd/multipass/releases
[issue]: https://github.com/CanonicalLtd/multipass/issues/577
