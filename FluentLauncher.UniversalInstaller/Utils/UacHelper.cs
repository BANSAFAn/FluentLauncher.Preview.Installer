using FluentLauncher.UniversalInstaller.Pages;
using System;
using System.Diagnostics;
using System.Security.Principal;

namespace FluentLauncher.UniversalInstaller.Utils;

class UacHelper
{
    static public bool IsRunningAsAdministrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    static public void RunAsAdministrator(ComponentPageVM pageVM)
    {
        string args = "command";

        if (pageVM.ConnectXExtensionChecked)
            args += " --install-connectx-extension";

        if (pageVM.DotNet9Checked)
            args += " --install-dotnet9";

        ProcessStartInfo startInfo = new()
        {
            FileName = Process.GetCurrentProcess().MainModule.FileName,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = args
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                return;
            else throw;
        }

        Environment.Exit(0);
    }
}
