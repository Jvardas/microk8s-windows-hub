# microk8s-windows-installer

Simple windows installer, in the form of a console application that uses multipass to install microk8s.

# Prerequisites
|||
|------------------|------------|
| Operating System | Windows 10 |
| Hypervisor       | Hyper-V    |

# Installation process

The app automatically downloads the latest multipass release for windows from [github][] and installs it silently on the background.

After the installation has completed, you will be prompted to restart your system so that Hyper-V gets enabled.
After rebooting, the installer automatically continues with the creation and launch of a VM with the name `test-alice` using the following command: `launch -n test-alice --cloud-init cloud-config.yaml`. Since multipass supports `cloud-init`, microk8s is installed in the spawned VM with the help of the cloud configuration file `cloud-config.yaml`

[github]: [https://github.com/CanonicalLtd/multipass/releases]
