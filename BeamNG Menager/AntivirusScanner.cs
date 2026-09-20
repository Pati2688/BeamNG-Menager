using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BeamNGModManager
{
    internal enum AntivirusScanStatus
    {
        Clean,
        ThreatDetected,
        Unavailable,
        Error
    }

    internal sealed class AntivirusScanResult
    {
        public AntivirusScanStatus Status { get; init; }

        public string Message { get; init; } = "";

        public string Details { get; init; } = "";
    }

    internal static class AntivirusScanner
    {
        private static readonly TimeSpan ScanTimeout =
            TimeSpan.FromMinutes(3);

        public static async Task<AntivirusScanResult> ScanFileAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) ||
                !File.Exists(filePath))
            {
                return new AntivirusScanResult
                {
                    Status = AntivirusScanStatus.Error,
                    Message = "Plik do skanowania nie istnieje."
                };
            }

            string? defenderPath =
                FindMicrosoftDefenderCommandLine();

            if (string.IsNullOrWhiteSpace(defenderPath))
            {
                return new AntivirusScanResult
                {
                    Status = AntivirusScanStatus.Unavailable,
                    Message =
                        "Nie znaleziono narzędzia Microsoft Defender (MpCmdRun.exe)."
                };
            }

            ProcessStartInfo startInfo =
                new ProcessStartInfo
                {
                    FileName = defenderPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            // Skan niestandardowy konkretnego pliku.
            // DisableRemediation powoduje, że Defender tylko raportuje wynik:
            // nie usuwa ani nie poddaje pliku kwarantannie podczas tego sprawdzenia.
            // W tym trybie Defender skanuje także zawartość archiwów.
            startInfo.ArgumentList.Add("-Scan");
            startInfo.ArgumentList.Add("-ScanType");
            startInfo.ArgumentList.Add("3");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-DisableRemediation");

            try
            {
                using Process process =
                    new Process
                    {
                        StartInfo = startInfo
                    };

                if (!process.Start())
                {
                    return new AntivirusScanResult
                    {
                        Status = AntivirusScanStatus.Error,
                        Message =
                            "Nie udało się uruchomić skanowania Microsoft Defender."
                    };
                }

                Task<string> outputTask =
                    process.StandardOutput.ReadToEndAsync();

                Task<string> errorTask =
                    process.StandardError.ReadToEndAsync();

                using CancellationTokenSource timeout =
                    new CancellationTokenSource(
                        ScanTimeout);

                using CancellationTokenSource linked =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken,
                        timeout.Token);

                try
                {
                    await process.WaitForExitAsync(
                        linked.Token);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(
                                entireProcessTree: true);
                        }
                    }
                    catch
                    {
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    return new AntivirusScanResult
                    {
                        Status = AntivirusScanStatus.Error,
                        Message =
                            "Skanowanie Microsoft Defender przekroczyło limit 3 minut."
                    };
                }

                string standardOutput =
                    await outputTask;

                string standardError =
                    await errorTask;

                string details =
                    (standardOutput +
                     Environment.NewLine +
                     standardError)
                    .Trim();

                if (process.ExitCode == 0)
                {
                    return new AntivirusScanResult
                    {
                        Status = AntivirusScanStatus.Clean,
                        Message =
                            "Microsoft Defender nie zgłosił zagrożeń.",
                        Details = details
                    };
                }

                if (LooksLikeThreatDetection(details))
                {
                    return new AntivirusScanResult
                    {
                        Status = AntivirusScanStatus.ThreatDetected,
                        Message =
                            "Microsoft Defender wykrył potencjalne zagrożenie w pliku.",
                        Details = details
                    };
                }

                return new AntivirusScanResult
                {
                    Status = AntivirusScanStatus.Error,
                    Message =
                        "Microsoft Defender nie zakończył skanowania poprawnie " +
                        "(kod " +
                        process.ExitCode +
                        ").",
                    Details = details
                };
            }
            catch (Exception ex)
            {
                return new AntivirusScanResult
                {
                    Status = AntivirusScanStatus.Error,
                    Message =
                        "Nie udało się uruchomić Microsoft Defender: " +
                        ex.Message
                };
            }
        }

        private static bool LooksLikeThreatDetection(
            string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            string text =
                output.ToLowerInvariant();

            if (text.Contains("found no threats") ||
                Regex.IsMatch(
                    text,
                    @"detected\s+0\s+threat"))
            {
                return false;
            }

            return
                Regex.IsMatch(
                    text,
                    @"detected\s+[1-9][0-9]*\s+threat") ||
                Regex.IsMatch(
                    text,
                    @"found\s+[1-9][0-9]*\s+threat") ||
                text.Contains("threat detected") ||
                text.Contains("threat was detected") ||
                text.Contains("malware found") ||
                text.Contains("malware was found");
        }

        private static string? FindMicrosoftDefenderCommandLine()
        {
            string commonApplicationData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData);

            string platformRoot =
                Path.Combine(
                    commonApplicationData,
                    "Microsoft",
                    "Windows Defender",
                    "Platform");

            try
            {
                if (Directory.Exists(platformRoot))
                {
                    string? newest =
                        Directory.GetDirectories(platformRoot)
                            .OrderByDescending(
                                path => Path.GetFileName(path),
                                StringComparer.OrdinalIgnoreCase)
                            .Select(
                                path => Path.Combine(
                                    path,
                                    "MpCmdRun.exe"))
                            .FirstOrDefault(File.Exists);

                    if (!string.IsNullOrWhiteSpace(newest))
                    {
                        return newest;
                    }
                }
            }
            catch
            {
            }

            string programFiles =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);

            string fallback =
                Path.Combine(
                    programFiles,
                    "Windows Defender",
                    "MpCmdRun.exe");

            return File.Exists(fallback)
                ? fallback
                : null;
        }
    }
}
