using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using FluentLauncher.UniversalInstaller.Pages;
using FluentLauncher.UniversalInstaller.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using FluentLauncher.UniversalInstaller.Properties;

namespace FluentLauncher.UniversalInstaller;

internal partial class ViewModel : ObservableRecipient, IRecipient<InstallResultMessage>, IRecipient<StartInstallMessage>
{
    internal int pageIndex = 0;

    public Page[] Pages =
    [
        new WelcomePage() { DataContext = new WelcomePageVM() },
        new LicensePage() { DataContext = new LicensePageVM() },
        new ComponentPage() { DataContext = new ComponentPageVM() },
        new ProgressPage(),
        new FinishPage()
    ];

    internal Frame Frame { get; set; }

    [ObservableProperty]
    public partial IBaseStepViewModel CurrentFrameDataContext { get; set; }

    [ObservableProperty]
    public partial bool CancelButtonEnable { get; set; } = true;

    public string NextButtonText => pageIndex switch
    {
        1 => Resources.AgreeAndContinue,
        2 => Resources.Install,
        4 => Resources.Finish,
        _ => Resources.Next
    };

    [RelayCommand]
    void Cancel(Window window)
    {
        MessageBoxResult result = MessageBox.Show(
            Resources.ConfirmExit.Replace("$(arch)", $"({SystemHelper.GetArchitecture()})"),
            Resources.MainWindowTitle.Replace("$(arch)", $"({SystemHelper.GetArchitecture()})"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            Environment.Exit(0);
    }

    public void Navigate()
    {
        OnPropertyChanged(nameof(NextButtonText));
        Frame.Navigate(Pages[pageIndex]);
    }

    [RelayCommand]
    void Next()
    {
        if (pageIndex == 2)
        {
            ComponentPageVM vm = (ComponentPageVM)CurrentFrameDataContext;

            if (!UacHelper.IsRunningAsAdministrator())
                UacHelper.RunAsAdministrator(vm);

            Pages[3].DataContext = new ProgressPageVM()
            {
                ConnectXExtensionChecked = vm.ConnectXExtensionChecked,
                DotNet9Checked = vm.DotNet9Checked
            };
        }
        else if (pageIndex == 3)
        {
            ProgressPageVM vm = (ProgressPageVM)CurrentFrameDataContext;

            Pages[4].DataContext = new FinishPageVM()
            {
                PackageFamilyName = vm.PackageFamilyName
            };
        }
        else if (pageIndex == 4)
        {
            (CurrentFrameDataContext as FinishPageVM).Finish();

            App.Current.Shutdown();
            return;
        }

        pageIndex++;
        Navigate();
    }

    [RelayCommand]
    void Back()
    {
        pageIndex--;
        Navigate();
    }

    void IRecipient<InstallResultMessage>.Receive(InstallResultMessage message) => App.Current.Dispatcher.Invoke(() =>
    {
        if (message.Value)
            Next();
        else CancelButtonEnable = true;
    });

    void IRecipient<StartInstallMessage>.Receive(StartInstallMessage _) 
        => App.Current.Dispatcher.Invoke(() => CancelButtonEnable = false);
}

internal interface IBaseStepViewModel
{
    bool CanNext { get; }

    bool CanBack { get; }
}

class InstallResultMessage(bool result) : ValueChangedMessage<bool>(result);

class StartInstallMessage();