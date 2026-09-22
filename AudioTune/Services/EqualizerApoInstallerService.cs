using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using AudioTune.Models;
using Microsoft.Win32;

namespace AudioTune.Services;

public sealed class EqualizerApoInstallerService
{
    public sealed record InstallResult(bool Installed, bool InstallerCompleted, string Message, string? InstallerPath = null);

    // Official Equalizer APO release hosted on SourceForge.
    // The SHA-256 below is published on the SourceForge download page for this exact file.
    public const string Version = "1.4.2";
    public const string X64DownloadUrl = "https://sourceforge.net/projects/equalizerapo/files/1.4.2/EqualizerAPO-x64-1.4.2.exe/download";
    public const string X64Sha256 = "7403be7427bbe1936a40dded082829b6e217fc4f5990fee5cba501f0ae055afa";

    private static readonly HttpClient Http = CreateHttpClient();

    public async Task<InstallResult> InstallAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Environment.Is64BitOperatingSystem)
            return new InstallResult(false, false, "AudioTune's integrated installer currently supports 64-bit Windows only.");

        var installerPath = GetInstallerPath();
        Directory.CreateDirectory(Path.GetDirectoryName(installerPath)!);

        try
        {
            bool validExisting = File.Exists(installerPath) && await VerifyHashAsync(installerPath, cancellationToken);
            if (!validExisting)
            {
                if (File.Exists(installerPath)) File.Delete(installerPath);
                await DownloadAsync(installerPath, progress, cancellationToken);

                if (!await VerifyHashAsync(installerPath, cancellationToken))
                {
                    File.Delete(installerPath);
                    return new InstallResult(false, false,
                        "The downloaded Equalizer APO installer failed SHA-256 verification and was deleted.");
                }
            }
            else
            {
                progress?.Report(1.0);
            }

            AppServices.Log.Log($"Equalizer APO {Version} installer verified (SHA-256) and ready.", LogLevel.Success);

            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(installerPath)!
            };

            Process? process;
            try
            {
                process = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new InstallResult(false, false, "Installation was cancelled at the Windows UAC prompt.", installerPath);
            }

            if (process is null)
                return new InstallResult(false, false, "The Equalizer APO installer could not be started.", installerPath);

            await process.WaitForExitAsync(cancellationToken);

            bool installed = FindInstallDirectory() is not null;
            var message = installed
                ? "Equalizer APO is installed. Select the desired playback device in its Configurator and reboot Windows if requested."
                : $"The installer exited with code {process.ExitCode}, but AudioTune cannot detect Equalizer APO yet.";

            AppServices.Log.Log(message, installed ? LogLevel.Success : LogLevel.Warning);
            return new InstallResult(installed, true, message, installerPath);
        }
        catch (OperationCanceledException)
        {
            return new InstallResult(false, false, "Equalizer APO installation was cancelled.", installerPath);
        }
        catch (Exception ex)
        {
            AppServices.Log.Log($"Equalizer APO installation failed: {ex.Message}", LogLevel.Error);
            return new InstallResult(false, false, ex.Message, installerPath);
        }
    }

    public bool LaunchConfigurator()
    {
        var path = FindConfiguratorPath();
        if (path is null) return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(path)!
            });
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    public string? FindConfiguratorPath()
    {
        var root = FindInstallDirectory();
        if (root is null) return null;

        // Newer builds may surface the device selector under either name.
        var candidates = new[]
        {
            Path.Combine(root, "Configurator.exe"),
            Path.Combine(root, "DeviceSelector.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public static string? FindInstallDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = hklm.OpenSubKey(@"SOFTWARE\EqualizerAPO");
                    var installPath = key?.GetValue("InstallPath")?.ToString();
                    if (!string.IsNullOrWhiteSpace(installPath))
                    {
                        installPath = Environment.ExpandEnvironmentVariables(installPath);
                        if (Directory.Exists(installPath)) return installPath;
                    }
                }
                catch { }
            }
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "EqualizerAPO")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AudioTune/0.4.18 (+https://sourceforge.net/projects/equalizerapo/)");
        return client;
    }

    private static string GetInstallerPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AudioTune", "Installers");
        return Path.Combine(dir, $"EqualizerAPO-x64-{Version}.exe");
    }

    private static async Task DownloadAsync(string destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(X64DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total is > 0) progress?.Report((double)received / total.Value);
        }
        progress?.Report(1.0);
    }

    private static async Task<bool> VerifyHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        return actual.Equals(X64Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
