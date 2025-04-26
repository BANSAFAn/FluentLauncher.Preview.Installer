using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FluentLauncher.UniversalInstaller.Models;
using FluentLauncher.UniversalInstaller.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;

namespace FluentLauncher.UniversalInstaller.Pages;

public partial class ProgressPage : Page
{
    ProgressPageVM VM => (ProgressPageVM)DataContext;

    public ProgressPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        VM.IsActive = true;
        VM.ExecuteAsync();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => VM.IsActive = false;
}

partial class ProgressPageVM : ObservableRecipient, IBaseStepViewModel
{
    private static HttpClient _httpClient = new();
    private readonly Dispatcher dispatcher = App.Current.Dispatcher;

    static ProgressPageVM()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0");
    }

    [ObservableProperty]
    public partial bool CanNext { get; set; }

    [ObservableProperty]
    public partial bool CanBack { get; set; }

    public bool ConnectXExtensionChecked { get; set; }

    public bool DotNet9Checked { get; set; }

    public string PackageName { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; }

    [ObservableProperty]
    public partial double ProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressVisibility))]
    [NotifyPropertyChangedFor(nameof(ExceptionVisibility))]
    public partial bool Failed { get; set; } = false;

    [ObservableProperty]
    public partial string ExceptionText { get; set; }

    public Visibility ProgressVisibility => !Failed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ExceptionVisibility => Failed ? Visibility.Visible : Visibility.Collapsed;

    public Task ExecuteAsync() => Task.Run(async () =>
    {
        this.Messenger.Send(new StartInstallMessage());

        UpdateProgress(0.0, "正在检索安装所需信息", true);
        (PublishModel publishModel, ReleaseModel releaseModel) = await GetLauncherPackageInformation();

        FileInfo fileInfo = await DownloadLauncherPackage(releaseModel);

        UpdateProgress(0.0, "正在导入启动器证书", true);
        ImportLauncherPackageCertification();

        UpdateProgress(0.0, "正在安装启动器包", true);
        await InstallLauncherPackage(fileInfo);

        //if (ConnectXExtensionChecked)
        //{

        //}

        if (DotNet9Checked)
        {
            FileInfo file = await DownloadDotNet9Package();

            UpdateProgress(0.0, "正在安装 .NET 9.0 Desktop Runtime (v9.0.4) 安装包", true);
            await InstallDotNet9Package(file);
        }

        UpdateProgress(100, "所有组件安装完成", false);
    }).ContinueWith(task => 
    {
        dispatcher.Invoke(() => Failed = task.IsFaulted);

        if (task.IsFaulted)
        {
            dispatcher.Invoke(() => ExceptionText = task.Exception.ToString());
        }

        this.Messenger.Send(new InstallResultMessage(!task.IsFaulted));
    });

    async Task<(PublishModel, ReleaseModel)> GetLauncherPackageInformation()
    {
        const string githubApi = "https://api.github.com/repos/Xcube-Studio/Natsurainko.FluentLauncher/releases";

        string releasesContent = await _httpClient.GetStringAsync(githubApi);
        string pattern = @"(?<=``` json)([\s\S]+?)(?=```)";
        var releases = JsonSerializer.Deserialize<ReleaseModel[]>(releasesContent)!
            .Where(releaseModel => releaseModel.TagName.Contains("pre-release") && releaseModel.IsPreRelease)
            .OrderByDescending(releaseModel => DateTime.Parse(releaseModel.PublishedAt))
            .ToArray();

        foreach (var releaseModel in releases)
        {
            Match match = Regex.Match(releaseModel.Body, pattern);

            if (!match.Success)
                continue;

            try
            {
                PublishModel publishModel = JsonSerializer.Deserialize<PublishModel>(match.Groups[1].Value);

                if (ConnectXExtensionChecked == publishModel.EnableLoadExtension)
                    return (publishModel, releaseModel);
            }
            catch { }
        }

        throw new Exception($"没有找到符合条件的版本");
    }

    async Task<FileInfo> DownloadLauncherPackage(ReleaseModel releaseModel)
    {
        string architecture = SystemHelper.GetArchitecture();
        string downloadUrl = "https://source.cubestructor.cc/" + releaseModel.Assets
            .FirstOrDefault(x => x.Name == $"updatePackage-{architecture}.zip")
            .DownloadUrl;

        return await HttpHelper.Download(downloadUrl, $"updatePackage-{architecture}.zip", p =>
        {
            UpdateProgress(p, "正在下载启动器安装包", false);
        });
    }

    void ImportLauncherPackageCertification()
    {
        const string Certification = """
            MIIDHDCCAgSgAwIBAgIQJ9btv5s1GrpAYNuif4HC8TANBgkqhkiG9w0BAQsFADAv
            MS0wKwYDVQQDEyQwNTNFRkIwRS02NzA1LTRBMTEtOTRCOS05ODBDNEM5RTAwNDcw
            HhcNMjQwNzAxMDYyNDEzWhcNMjUwNzAxMTIyNDEzWjAvMS0wKwYDVQQDEyQwNTNF
            RkIwRS02NzA1LTRBMTEtOTRCOS05ODBDNEM5RTAwNDcwggEiMA0GCSqGSIb3DQEB
            AQUAA4IBDwAwggEKAoIBAQDU/dm+LIjWovZNGdfkkFvsGkNLrDWWLWlz6MuZC4BV
            nedv3NyIYJxUt9WMo8BXdemQ7NqGRjtzthSGA1E8fB+UmC15lfMJZJ3L2nUIZG1d
            dOXP1/XW/3Xo97Qzm6Orx3AfJa3jSSRuflbUSvW0l6eEHsUTMYqa31lYiebI03H+
            eia8eVbgxzaMhmjHYEkv7gElFq5OtTIvZ4QONah/ijFeoFFTkvrFOxZtelcOqMRW
            P7E1I0dpZGswI68wmPfCdIcgTAj60JXy4N94mP7WMvdlIJvSFRM/CIvMUOP/ITYR
            YoWawI88rRplweQSNt6DnLy11Gz+rAboULUbKKiV3xlFAgMBAAGjNDAyMAwGA1Ud
            EwEB/wQCMAAwIgYDVR0lAQH/BBgwFgYIKwYBBQUHAwMGCisGAQQBgjdUAwEwDQYJ
            KoZIhvcNAQELBQADggEBADOL+F24yg9fqptdtGip4ZtlRvDfUTp3inYV2JUk3vHw
            nMjHQmGIFAEOifcXXh6E6T/MjepdvQIRMTjnVLinAPvq8Y2sKh5SQZCxED/rBukv
            Hmw1PElTMxrVo/fUOE5ASyZvcZ63EmeiFffYpjXkZBsT54e88BgOh2WP1D2Rczsx
            WhyLZufOx+nwa7KaH9shACa1va8PnPlQiNx5ynxkicXyVaAcpa0wVFwl8k+0qmLX
            RLnnzK2fbXQRfyIhKG1q1rZ6V2O8N8Xf19WiZmqVzxxm9EjVKVXMl5uG7Y/MVKOw
            ibZ+jVh3oqn2yxqQ/zCt5e//A5dUWPzIhA0f5eGaywQ=
            """;

        byte[] certificateBytes = Convert.FromBase64String(Certification);

        using X509Certificate2 x509Certificate = new(certificateBytes);
        using X509Store store = new(StoreName.TrustedPeople, StoreLocation.LocalMachine);

        store.Open(OpenFlags.ReadWrite);
        store.Remove(x509Certificate);
        store.Add(x509Certificate);
    }

    async Task InstallLauncherPackage(FileInfo fileInfo)
    {
        try
        {
            string architecture = SystemHelper.GetArchitecture();

            DirectoryInfo directory = new(Path.Combine(fileInfo.DirectoryName, $"updatePackage-{architecture}"));

            if (directory.Exists)
                directory.Delete(true);

            directory.Create();

            ZipFile.ExtractToDirectory(fileInfo.FullName, directory.FullName);

            string packagePath = Path.Combine(directory.FullName, $"msix-{architecture}.msix");
            string[] dependencyPackagesPath = Directory.GetFiles(directory.FullName, "dependencies");

            #region Parse Package

            string packageName = null;

            using (var zipArchive = ZipFile.Open(packagePath, ZipArchiveMode.Read))
            {
                using var stream = zipArchive.Entries.First(entry => entry.FullName.EndsWith("AppxManifest.xml")).Open();
                using var streamReader = new StreamReader(stream);

                using var xmlReader = XmlReader.Create(streamReader);
                packageName = PackageName = xmlReader.ReadToDescendant("Identity")
                    ? xmlReader.GetAttribute("Name")
                    : null;
            }

            #endregion

            #region Check If Commands Exists

            static void CheckPowerShellCommand(string commandName)
            {
                using var process = Process.Start(new ProcessStartInfo("powershell", $"Get-Command {commandName}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }) ?? throw new InvalidOperationException("couldn't start powershell process");

                process.WaitForExit();

                if (process.ExitCode != 0)
                    new NotSupportedException($"{commandName} Command does not exist");
            }

            CheckPowerShellCommand("Add-AppxPackage");
            CheckPowerShellCommand("Get-AppxPackage");

            #endregion

            #region Check If Package Installed

            bool isPackageInstalled = false;
            string packageFamilyName = null;

            using (var process = Process.Start(new ProcessStartInfo("powershell", $"Get-AppxPackage -Name {packageName}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("couldn't start powershell process"))
            {
                process.WaitForExit();
                string content = await process.StandardOutput.ReadToEndAsync();

                if (!string.IsNullOrEmpty(content))
                {
                    packageFamilyName = content
                        .Split('\n')
                        .FirstOrDefault(line => line.Contains("PackageFamilyName"))?
                        .Split(':')[1]?
                        .Trim();

                    isPackageInstalled = true;
                }
            }

            #endregion

            #region Install Dependency Packages

            foreach (string path in dependencyPackagesPath)
            {
                using var process = Process.Start(new ProcessStartInfo("powershell", $"Add-AppxPackage -Path \"{path}\" -ForceUpdateFromAnyVersion -ForceApplicationShutdown")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }) ?? throw new InvalidOperationException("couldn't start powershell process");

                process.WaitForExit();
                string errors = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(errors))
                {
                    if (errors.Contains("0x80073D06")) // Package already installed as higher version
                        continue;

                    throw new InvalidOperationException($"counldn't install dependency package {path}: {errors}");
                }
            }

            #endregion

            #region Install/Update Package

            string forceUpdateOption = isPackageInstalled ? " -ForceUpdateFromAnyVersion" : string.Empty;
            using (var process = Process.Start(new ProcessStartInfo("powershell", $"Add-AppxPackage -Path \"{packagePath}\" -ForceApplicationShutdown" + forceUpdateOption)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("couldn't start powershell process"))
            {
                process.WaitForExit();
                string errors = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(errors))
                    throw new InvalidOperationException($"counldn't install or update package {packagePath}: {errors}");
            }

            #endregion
        }
        finally
        {
            fileInfo.Delete();
        }
    }

    async Task<FileInfo> DownloadDotNet9Package()
    {
        string architecture = SystemHelper.GetArchitecture();
        string downloadUrl = $"https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.4/windowsdesktop-runtime-9.0.4-win-{architecture}.exe";

        return await HttpHelper.Download(downloadUrl, $"updatePackage-{architecture}.zip", p =>
        {
            UpdateProgress(p, "正在下载 .NET 9.0 Desktop Runtime (v9.0.4) 安装包", false);
        });
    }

    async Task InstallDotNet9Package(FileInfo fileInfo)
    {
        try
        {
            ProcessStartInfo processStartInfo = new()
            {
                FileName = fileInfo.FullName,
                Arguments = "/install /quiet /norestart",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);

            process.WaitForExit();

            string errors = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"counldn't install .NET 9.0 Desktop Runtime (v9.0.4): {errors}");
        }
        finally
        {
            fileInfo.Delete();
        }
    }

    void UpdateProgress(double progress, string text, bool isIndeterminate)
    {
        dispatcher.Invoke(() =>
        {
            ProgressValue = progress;
            ProgressText = text;
            IsIndeterminate = isIndeterminate;
        });
    }
}