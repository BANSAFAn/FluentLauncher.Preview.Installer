using FluentLauncher.UniversalInstaller.Utils;
using System.Windows.Controls;

namespace FluentLauncher.UniversalInstaller.Pages;

public partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        WelcomeText.Text = WelcomeText.Text.Replace("$(arch)", $"({SystemHelper.GetArchitecture()})");
    }
}

class WelcomePageVM : IBaseStepViewModel
{
    public bool CanNext => true;

    public bool CanBack => false;
}