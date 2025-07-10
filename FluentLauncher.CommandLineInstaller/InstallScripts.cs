using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace FluentLauncher.CommandLineInstaller;

public class InstallScripts
{
    public const string Certification = """
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

    public static async Task InstallPackage(string packagePath, string[] dependencyPackagesPath,
        string? certificationPath = null, bool launchAfterInstalled = true, string? logFilePath = default)
    {
        TextWriter defaultWriter = Console.Out;
        StreamWriter? streamWriter = null;

        if (!string.IsNullOrEmpty(logFilePath))
        {
            try
            {
                FileInfo logFile = new(logFilePath);
                streamWriter = logFile.CreateText();

                Console.SetOut(streamWriter);
                Console.SetError(streamWriter);
            }
            catch { }
        }

        try
        {
            #region Parse Package

            string? packageName = null;

            using (var zipArchive = ZipFile.Open(packagePath, ZipArchiveMode.Read))
            {
                using var stream = zipArchive.Entries.First(entry => entry.FullName.EndsWith("AppxManifest.xml")).Open();
                using var streamReader = new StreamReader(stream);

                using var xmlReader = XmlReader.Create(streamReader);
                packageName = xmlReader.ReadToDescendant("Identity")
                    ? xmlReader.GetAttribute("Name")
                    : null;
            }

            Console.WriteLine($"Parsed Package Name: {packageName}");

            #endregion

            #region Check If Commands Exists

            static async Task CheckPowerShellCommand(string commandName)
            {
                using var process = Process.Start(new ProcessStartInfo("powershell", $"Get-Command {commandName}")
                {
                    CreateNoWindow = true,
                }) ?? throw new InvalidOperationException("couldn't start powershell process");

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"PowerShell Command [{commandName}]: Missing");
                    throw new NotSupportedException($"{commandName} Command does not exist");
                }
                else Console.WriteLine($"PowerShell Command [{commandName}]: Checked");
            }

            await CheckPowerShellCommand("Add-AppxPackage");
            await CheckPowerShellCommand("Get-AppxPackage");

            #endregion

            #region Check If Package Installed

            bool isPackageInstalled = false;
            string? packageFamilyName = null;

            using (var process = Process.Start(new ProcessStartInfo("powershell", $"Get-AppxPackage -Name {packageName}")
            { 
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("couldn't start powershell process"))
            {
                await process.WaitForExitAsync();
                string content = await process.StandardOutput.ReadToEndAsync();

                if (!string.IsNullOrEmpty(content))
                {
                    packageFamilyName = content.Split('\n').FirstOrDefault(line => line.Contains("PackageFamilyName"))?.Split(":")[1]?.Trim();

                    Console.WriteLine($"Found Package {packageFamilyName} Installed");
                    isPackageInstalled = true;
                }
            };

            #endregion

            #region Install Dependency Packages

            foreach (string path in dependencyPackagesPath)
            {
                using (var process = Process.Start(new ProcessStartInfo("powershell", $"Add-AppxPackage -Path \"{path}\" -ForceUpdateFromAnyVersion -ForceApplicationShutdown")
                { 
                    RedirectStandardOutput = true, 
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }) ?? throw new InvalidOperationException("couldn't start powershell process"))
                {
                    await process.WaitForExitAsync();
                    string errors = await process.StandardError.ReadToEndAsync();

                    if (!string.IsNullOrEmpty(errors))
                    {
                        if (errors.Contains("0x80073D06")) // Package already installed as higher version
                        {
                            Console.WriteLine($"Dependency Package [{path}] Already Installed");
                            continue;
                        }

                        Console.Error.WriteLine($"Dependency Package [{path}] Installation Error:");
                        Console.Error.WriteLine(errors);

                        throw new InvalidOperationException($"counldn't install dependency package {path}");
                    }
                    else Console.WriteLine($"Dependency Package [{path}] Installed");
                };
            }

            #endregion

            #region Import Certification

            byte[] certificateBytes = certificationPath != null
                ? await File.ReadAllBytesAsync(certificationPath)
                : Convert.FromBase64String(Certification);

            using (var certificate = X509CertificateLoader.LoadCertificate(certificateBytes))
            {
                using X509Store store = new(StoreName.TrustedPeople, StoreLocation.LocalMachine);

                store.Open(OpenFlags.ReadWrite);
                store.Remove(certificate);
                store.Add(certificate);

                Console.WriteLine("Certification Imported");
            }

            #endregion

            #region Install/Update Package

            string forceUpdateOption = isPackageInstalled ? " -ForceUpdateFromAnyVersion" : string.Empty;
            using (var process = Process.Start(new ProcessStartInfo("powershell", $"Add-AppxPackage -Path \"{packagePath}\" -ForceApplicationShutdown" + forceUpdateOption)
            { 
                RedirectStandardOutput = true, 
                RedirectStandardError = true,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("couldn't start powershell process"))
            {
                await process.WaitForExitAsync();
                string errors = await process.StandardError.ReadToEndAsync();

                if (!string.IsNullOrEmpty(errors))
                {
                    Console.Error.WriteLine($"Application Package [{packagePath}] Installation Error:");
                    Console.Error.WriteLine(errors);

                    throw new InvalidOperationException($"counldn't install or update package {packagePath}");
                }
                else Console.WriteLine($"Application Package [{packagePath}] Installed/Updated");
            };

            #endregion

            #region Launch Application

            try
            {
                using (var process = Process.Start(new ProcessStartInfo("powershell", $"Get-AppxPackage -Name {packageName}")
                { 
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }) ?? throw new InvalidOperationException("couldn't start powershell process"))
                {
                    await process.WaitForExitAsync();
                    string content = await process.StandardOutput.ReadToEndAsync();

                    if (string.IsNullOrEmpty(content))
                        throw new InvalidOperationException("counldn't get PackageFamilyName of installed package");

                    packageFamilyName = content.Split('\n').FirstOrDefault(line => line.Contains("PackageFamilyName"))?.Split(":")[1]?.Trim();
                };

                if (launchAfterInstalled)
                    Process.Start("explorer.exe", $"shell:AppsFolder\\{packageFamilyName}!App");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed to Launch Application");
                Console.Error.WriteLine(ex);
            }

            #endregion
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            streamWriter?.Flush();
            streamWriter?.Close();

            Console.SetOut(defaultWriter);
            Console.SetError(defaultWriter);

            throw;
        }

        streamWriter?.Flush();
        streamWriter?.Close();
    }
}
