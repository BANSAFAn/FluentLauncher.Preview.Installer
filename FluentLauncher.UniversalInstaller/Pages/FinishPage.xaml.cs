using CommunityToolkit.Mvvm.ComponentModel;
using FluentLauncher.UniversalInstaller.Utils;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FluentLauncher.UniversalInstaller.Properties;

namespace FluentLauncher.UniversalInstaller.Pages;

public partial class FinishPage : Page
{
    FinishPageVM VM => (FinishPageVM)DataContext;

    public FinishPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        FinishText.Text = FinishText.Text.Replace("$(arch)", $"({SystemHelper.GetArchitecture()})");
    }
}

partial class FinishPageVM : ObservableObject, IBaseStepViewModel
{
    public bool CanNext => true;

    public bool CanBack => false;

    public string PackageFamilyName { get; set; }

    [ObservableProperty]
    public partial bool OpenLauncher { get; set; } = true;

    public void Finish()
    {
        if (!OpenLauncher)
            return;

        try
        {
            Process.Start("explorer.exe", $"shell:AppsFolder\\{PackageFamilyName}!App");
        }
        catch (Exception ex)
        {
            MessageBox.Show(string.Format(Resources.FailedToLaunch, ex));
        }
    }
}
