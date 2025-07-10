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

    public string PackageFamilyName { get; set; }

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

        if (ConnectXExtensionChecked)
        {
            FileInfo file = await DownloadConnectXExtension();

            UpdateProgress(0.0, "正在安装 .NET 9.0 Desktop Runtime (v9.0.4) 安装包", true);
            InstallConnectXExtension(file);
        }

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

                if (ConnectXExtensionChecked == publishModel.EnableLoadExtensions)
                    return (publishModel, releaseModel);
            }
            catch { }
        }

        throw new Exception($"没有找到符合条件的版本");
    }

    async Task<FileInfo> DownloadLauncherPackage(ReleaseModel releaseModel)
    {
        string architecture = SystemHelper.GetArchitecture();
        string downloadUrl = "https://source.xcubestudio.net/" + releaseModel.Assets
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
            MIIDLjCCAhagAwIBAgIQXpBeM3gATJRP7k7Qo5VHBzANBgkqhkiG9w0BAQsFADAv
            MS0wKwYDVQQDDCQwNTNFRkIwRS02NzA1LTRBMTEtOTRCOS05ODBDNEM5RTAwNDcw
            HhcNMjUwNzEwMDY0ODI3WhcNMzUwNzEwMDY1ODI2WjAvMS0wKwYDVQQDDCQwNTNF
            RkIwRS02NzA1LTRBMTEtOTRCOS05ODBDNEM5RTAwNDcwggEiMA0GCSqGSIb3DQEB
            AQUAA4IBDwAwggEKAoIBAQCyBbAwrLWaFa49WwSQdjWES7/z4rxSK21HM1PtKqi4
            WROD6CtbUVu4o+U0/mjuDJhwIAczbLs77v0QgrwPBgZjwaXYmzUdfQ04hFXMO65R
            OZF3R8yDDFRVYdPTO9RaYEaDxfvctPp5g/+N+ucicLlFZy7ozwxoHlfex1IBmBSz
            niMGAKceM7FM4y9+wGv49WRsj089uC0mAwOxeE2sdEzSUxt0E8YEKxsLp3kuv2t5
            5x3JyaY/GrXlZ9tfLkIapAYyEsrIBsa2ZaVyuzioutyx3L/Wh42Yw7vtbGgHJJyo
            2cJV0GPCiUNB5aJjE5HQhdUnKE/3u1Y6PMyP9vZDhcqFAgMBAAGjRjBEMA4GA1Ud
            DwEB/wQEAwIHgDATBgNVHSUEDDAKBggrBgEFBQcDAzAdBgNVHQ4EFgQUwtZUvCc6
            pvVyLSmPgrEuUsFM9dIwDQYJKoZIhvcNAQELBQADggEBAFrFRjED4UokwcG0Maps
            v/CJsHG+KftBN/p6yXlcHTGnMSan4CMG1JgNPjQOWpGxTQV9cH/A/kqGUCc9QVwW
            AX3GVWeGacyhhnokgA1q9qEQNXYxdequJllt2axnHnM9V6B5EW5Z7Te/eyEKHChG
            02u1Wx906rsXbFd04jlXvtWpzIeRzHjGGeKkC9gcYmP6ze4zNReSiq/u8oduyXfb
            /Hz65ehtHUWsiKZYDiic4XPKNp8uGQQRmNKTrKFynaOAM4p5ffe/55XwDByskmGA
            YbMiGZumh4bltjiXFF/81J1Cg5kJokOydIxjgE9WXmZwU7+770PCaLwlPN9hZHVx
            i+E=
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
            string[] dependencyPackagesPath = Directory.GetFiles(Path.Combine(directory.FullName, "dependencies"));

            #region Parse Package

            string packageName = null;

            using (var zipArchive = ZipFile.Open(packagePath, ZipArchiveMode.Read))
            {
                using var stream = zipArchive.Entries.First(entry => entry.FullName.EndsWith("AppxManifest.xml")).Open();
                using var streamReader = new StreamReader(stream);

                using var xmlReader = XmlReader.Create(streamReader);
                packageName = xmlReader.ReadToDescendant("Identity")
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
                using var process = Process.Start(new ProcessStartInfo("powershell", $"-Command \"Add-AppxPackage -Path \'{path}\' -ForceUpdateFromAnyVersion -ForceApplicationShutdown\"")
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

            string forceUpdateOption = isPackageInstalled ? "-ForceUpdateFromAnyVersion" : string.Empty;
            using (var process = Process.Start(new ProcessStartInfo("powershell", $"-Command \"Add-AppxPackage -Path \'{packagePath}\' -ForceApplicationShutdown {forceUpdateOption}\"")
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

            #region Get PackageFamilyName

            using (var process = Process.Start(new ProcessStartInfo("powershell", $"Get-AppxPackage -Name {packageName}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("couldn't start powershell process"))
            {
                process.WaitForExit();
                string content = await process.StandardOutput.ReadToEndAsync();

                if (string.IsNullOrEmpty(content))
                    throw new InvalidOperationException("counldn't get PackageFamilyName of installed package");

                packageFamilyName = content
                    .Split('\n')
                    .FirstOrDefault(line => line.Contains("PackageFamilyName"))?
                    .Split(':')[1]?
                    .Trim();

                PackageFamilyName = packageFamilyName;
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

    async Task<FileInfo> DownloadConnectXExtension()
    {
        const string githubApi = "https://api.github.com/repos/Xcube-Studio/FluentLauncher.Extension.ConnectX/releases/latest";
        string releasesContent = await _httpClient.GetStringAsync(githubApi);
        string architecture = SystemHelper.GetArchitecture();

        string downloadUrl = "https://source.xcubestudio.net/" + JsonSerializer.Deserialize<ReleaseModel>(releasesContent).Assets
            .First(a => a.Name.EndsWith($"{architecture}.zip"))
            .DownloadUrl;

        return await HttpHelper.Download(downloadUrl, $"FluentLauncher.Extension.ConnectX.{architecture}.zip", p =>
        {
            UpdateProgress(p, "正在下载 适用于 Fluent Launcher 的 ConnectX 扩展包", false);
        });
    }

    void InstallConnectXExtension(FileInfo fileInfo)
    {
        string directory = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Packages\\{PackageFamilyName}\\LocalState\\Extensions";
        string extensionPath = Path.Combine(directory, "FluentLauncher.Extension.ConnectX");

        if (Directory.Exists(extensionPath))
            Directory.Delete(extensionPath, true);

        Directory.CreateDirectory(extensionPath);
        ZipFile.ExtractToDirectory(fileInfo.FullName, extensionPath);
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