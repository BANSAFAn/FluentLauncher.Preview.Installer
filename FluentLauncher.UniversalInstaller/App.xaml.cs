using System.Windows;

namespace FluentLauncher.UniversalInstaller;

public partial class App : Application
{
    public static string[] Args { get; private set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Args = e.Args;
    }
}
