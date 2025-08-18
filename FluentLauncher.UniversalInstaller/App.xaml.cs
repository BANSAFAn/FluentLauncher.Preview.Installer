using System.Windows;
using System.Globalization;
using System.Threading;

namespace FluentLauncher.UniversalInstaller;

public partial class App : Application
{
    public static string[] Args { get; private set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Args = e.Args;

        var culture = CultureInfo.CurrentCulture;
        string lang = culture.TwoLetterISOLanguageName.ToLower();
        CultureInfo uiCulture;
        switch (lang)
        {
            case "ru": uiCulture = new CultureInfo("ru"); break;
            case "uk": uiCulture = new CultureInfo("uk"); break;
            case "de": uiCulture = new CultureInfo("de"); break;
            case "en": uiCulture = new CultureInfo("en"); break;
            case "zh": uiCulture = new CultureInfo("zh-Hans"); break;
            default: uiCulture = new CultureInfo("zh-Hans"); break;
        }
        Thread.CurrentThread.CurrentUICulture = uiCulture;
    }
}
