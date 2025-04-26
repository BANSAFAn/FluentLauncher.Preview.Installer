using FluentLauncher.UniversalInstaller.Pages;
using FluentLauncher.UniversalInstaller.Utils;
using System.CommandLine;
using System.Windows;
using System.Windows.Controls;

namespace FluentLauncher.UniversalInstaller;

public partial class MainWindow : Window
{
    ViewModel VM => (ViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        this.DataContext = new ViewModel();
        PrepareFrame();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        VM.Frame = this.Frame;
        VM.IsActive = true;
        VM.Navigate();

        this.UacImage.Source = UacIcon.GetUACIcon();

        if (UacHelper.IsRunningAsAdministrator())
            this.Title += " (管理员)";

        this.Title = this.Title.Replace("$(arch)", $"({SystemHelper.GetArchitecture()})");
    }

    private void Window_Unloaded(object sender, RoutedEventArgs e) => VM.IsActive = false;

    private void Frame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        Page page = Frame.Content as Page;

        VM.CurrentFrameDataContext = page.DataContext as IBaseStepViewModel;
        UacImage.Visibility = page is ComponentPage
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (VM.pageIndex == 4)
        {
            (VM.CurrentFrameDataContext as FinishPageVM).Finish();
            return;
        }

        if (!VM.CancelButtonEnable)
        {
            e.Cancel = true;

            MessageBox.Show(
                $"安装过程中不能退出",
                $"预览通道 Fluent Launcher ({SystemHelper.GetArchitecture()}) 安装",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            $"你确定要退出 预览版 Fluent Launcher ({SystemHelper.GetArchitecture()}) 安装向导吗？",
            $"预览通道 Fluent Launcher ({SystemHelper.GetArchitecture()}) 安装", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.No)
            e.Cancel = true;
    }

    void PrepareFrame()
    {
        var rootCommand = new RootCommand();

        Option<bool> connectxExtensionChecked = new(name: "--install-connectx-extension", getDefaultValue: () => false) { IsRequired = false, };
        Option<bool> dotnet9Checked = new(name: "--install-dotnet9", getDefaultValue: () => false) { IsRequired = false, };

        var command = new Command("command");
        command.AddOption(connectxExtensionChecked);
        command.AddOption(dotnet9Checked);
        command.SetHandler((c, d) =>
        {
            VM.Pages[1] = new LicensePage()
            {
                DataContext = new LicensePageVM()
                {
                    AcceptedLicense = true
                }
            };

            VM.Pages[2] = new ComponentPage()
            {
                DataContext = new ComponentPageVM()
                {
                    ConnectXExtensionChecked = c,
                    DotNet9Checked = d
                }
            };

            VM.Pages[3] = new ProgressPage()
            {
                DataContext = new ProgressPageVM()
                {
                    ConnectXExtensionChecked = c,
                    DotNet9Checked = d
                }
            };

            VM.pageIndex = 3;
        }, connectxExtensionChecked, dotnet9Checked);

        rootCommand.Add(command);
        rootCommand.SetHandler(() => { });

        rootCommand.Invoke(App.Args);
    }
}
