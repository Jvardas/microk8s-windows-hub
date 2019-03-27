# microk8s-windows-installer
Simple installer, in the form of a console application, in windows that will use multipass to install microk8s.

# process
After launching the app will automatically download the latest .exe of multipass from https://github.com/CanonicalLtd/multipass/releases, and then install it silently on the background. (After that the user will be prompt to reboot his computer since windows 10 will need to enable HyperV in order to host the virtual machines. After rebooting the installer will re-open automatically and continue with launching a VM, namely test-alice, with the command 'launch -n test-alice --cloud-init cloud-coonfig.yaml', which will automatically install microk8s in the VM).
