using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace BeamNGModManager
{
    public partial class MainWindow
    {
        private async Task<bool> ScanLocalModSecurityAsync(
            string filePath)
        {
            StatusText.Text =
                Localization.T(
                    "Skanowanie pliku przez Microsoft Defender...",
                    "Scanning file with Microsoft Defender...");

            AntivirusScanResult result =
                await AntivirusScanner.ScanFileAsync(
                    filePath);

            if (result.Status ==
                AntivirusScanStatus.Clean)
            {
                StatusText.Text =
                    Localization.T(
                        "Skan bezpieczeństwa zakończony — nie wykryto zagrożeń.",
                        "Security scan completed — no threats detected.");

                return true;
            }

            if (result.Status ==
                AntivirusScanStatus.ThreatDetected)
            {
                MessageBox.Show(
                    Localization.T(
                        "Instalacja została zablokowana.\n\n" +
                        "Microsoft Defender wykrył potencjalne zagrożenie w tym pliku.\n\n" +
                        "Plik nie został skopiowany do folderu modów.",
                        "Installation was blocked.\n\n" +
                        "Microsoft Defender detected a potential threat in this file.\n\n" +
                        "The file was not copied to the mods folder."),
                    Localization.T(
                        "Wykryto zagrożenie",
                        "Threat detected"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusText.Text =
                    Localization.T(
                        "Instalacja zablokowana przez skan bezpieczeństwa.",
                        "Installation blocked by security scan.");

                return false;
            }

            MessageBoxResult answer =
                MessageBox.Show(
                    Localization.T(
                        "Nie udało się potwierdzić bezpieczeństwa pliku za pomocą Microsoft Defender.\n\n" +
                        result.Message +
                        "\n\nCzy mimo to kontynuować instalację?",
                        "Microsoft Defender could not confirm that the file is safe.\n\n" +
                        result.Message +
                        "\n\nContinue installation anyway?"),
                    Localization.T(
                        "Nie udało się wykonać skanowania",
                        "Security scan failed"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            return answer ==
                MessageBoxResult.Yes;
        }

        private async Task ScanDownloadedModSecurityAsync(
            string filePath,
            string modTitle)
        {
            CatalogStatusText.Text =
                Localization.T(
                    "Skanowanie bezpieczeństwa „" +
                    modTitle +
                    "”...",
                    "Security scanning “" +
                    modTitle +
                    "”...");

            AntivirusScanResult result =
                await AntivirusScanner.ScanFileAsync(
                    filePath);

            if (result.Status ==
                AntivirusScanStatus.Clean)
            {
                CatalogStatusText.Text =
                    Localization.T(
                        "Microsoft Defender: brak wykrytych zagrożeń.",
                        "Microsoft Defender: no threats detected.");

                return;
            }

            if (result.Status ==
                AntivirusScanStatus.ThreatDetected)
            {
                throw new InvalidDataException(
                    Localization.T(
                        "Instalacja zablokowana: Microsoft Defender wykrył potencjalne zagrożenie w pobranym pliku.",
                        "Installation blocked: Microsoft Defender detected a potential threat in the downloaded file."));
            }

            MessageBoxResult answer =
                MessageBox.Show(
                    Localization.T(
                        "Nie udało się potwierdzić bezpieczeństwa pobranego moda za pomocą Microsoft Defender.\n\n" +
                        result.Message +
                        "\n\nCzy mimo to kontynuować instalację?",
                        "Microsoft Defender could not confirm that the downloaded mod is safe.\n\n" +
                        result.Message +
                        "\n\nContinue installation anyway?"),
                    Localization.T(
                        "Nie udało się wykonać skanowania",
                        "Security scan failed"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (answer !=
                MessageBoxResult.Yes)
            {
                throw new OperationCanceledException(
                    Localization.T(
                        "Instalacja anulowana po nieudanym skanowaniu bezpieczeństwa.",
                        "Installation cancelled after the security scan failed."));
            }
        }
    }
}
