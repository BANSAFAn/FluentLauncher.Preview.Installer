using System;
using System.Runtime.InteropServices;

namespace FluentLauncher.UniversalInstaller.Utils;

internal class SystemHelper
{
    public static string GetArchitecture() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        _ => throw new NotSupportedException("not supported architecture")
    };
}
