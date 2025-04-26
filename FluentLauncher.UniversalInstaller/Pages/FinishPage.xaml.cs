using CommunityToolkit.Mvvm.ComponentModel;
using FluentLauncher.UniversalInstaller.Utils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

    public string PackageName { get; set; }

    [ObservableProperty]
    public partial bool OpenLauncher { get; set; } = true;

    public async void Finish()
    {
        if (!OpenLauncher)
            return;

        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell", $"Get-AppxPackage -Name {PackageName}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("couldn't start powershell process");

            process.WaitForExit();
            string content = await process.StandardOutput.ReadToEndAsync();

            if (string.IsNullOrEmpty(content))
                throw new InvalidOperationException("counldn't get PackageFamilyName of installed package");

            string packageFamilyName = content
                .Split('\n')
                .FirstOrDefault(line => line.Contains("PackageFamilyName"))?
                .Split(':')[1]?
                .Trim();

            Process.Start("explorer.exe", $"shell:AppsFolder\\{packageFamilyName}!App");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to Launch Application: {ex}");
        }
    }
}
