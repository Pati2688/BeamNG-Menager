using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace BeamNGModManager
{
    public partial class MainWindow : Window
    {
        private string? modsFolder;
        private string gameVersion = "Nieznana";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            string beamNgBaseFolder =
                Path.Combine(localAppData, "BeamNG");

            string iniPath =
                Path.Combine(beamNgBaseFolder, "BeamNG.drive.ini");

            string userFolder =
                Path.Combine(
                    beamNgBaseFolder,
                    "BeamNG.drive",
                    "current");

            gameVersion = "Nieznana";

            if (File.Exists(iniPath))
            {
                try
                {
                    foreach (string line in File.ReadAllLines(iniPath))
                    {
                        string trimmed = line.Trim();

                        if (trimmed.StartsWith(
                            "version=",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            string value = trimmed
                                .Substring("version=".Length)
                                .Trim()
                                .Trim('"');

                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                gameVersion = value;
                            }
                        }

                        if (trimmed.StartsWith(
                            "userFolder=",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            string value = trimmed
                                .Substring("userFolder=".Length)
                                .Trim()
                                .Trim('"');

                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                if (Path.IsPathRooted(value))
                                {
                                    userFolder = value;
                                }
                                else
                                {
                                    userFolder = Path.GetFullPath(
                                        Path.Combine(
                                            beamNgBaseFolder,
                                            value));
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            string executableVersion = DetectGameVersionFromExecutable();

            if (!string.IsNullOrWhiteSpace(executableVersion))
            {
                gameVersion = executableVersion;
            }

            if (!Directory.Exists(userFolder))
            {
                StatusText.Text =
                    "Nie znaleziono folderu użytkownika BeamNG.\n\n" +
                    userFolder;

                ScanModsButton.IsEnabled = false;
                InstallModButton.IsEnabled = false;
                return;
            }

            modsFolder =
                Path.Combine(userFolder, "mods");

            if (!Directory.Exists(modsFolder))
            {
                Directory.CreateDirectory(modsFolder);
            }

            StatusText.Text =
                "Znaleziono BeamNG\n" +
                "Wersja gry: " + gameVersion + "\n" +
                "Folder modów: " + modsFolder;

            ScanModsButton.IsEnabled = true;
            InstallModButton.IsEnabled = true;
        }

        private string? DetectGameVersionFromExecutable()
        {
            string programFilesX86 =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86);

            string programFiles =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);

            string[] candidates =
            {
                Path.Combine(
                    programFilesX86,
                    "Steam",
                    "steamapps",
                    "common",
                    "BeamNG.drive",
                    "BeamNG.drive.x64.exe"),

                Path.Combine(
                    programFiles,
                    "Steam",
                    "steamapps",
                    "common",
                    "BeamNG.drive",
                    "BeamNG.drive.x64.exe")
            };

            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    FileVersionInfo info =
                        FileVersionInfo.GetVersionInfo(path);

                    string? version =
                        info.ProductVersion;

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        version = info.FileVersion;
                    }

                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        return version.Trim();
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private void ScanModsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ScanMods();
        }

        private void ScanMods()
        {
            if (string.IsNullOrWhiteSpace(modsFolder) ||
                !Directory.Exists(modsFolder))
            {
                StatusText.Text =
                    "Najpierw kliknij „Wykryj BeamNG”.";

                return;
            }

            List<ModInfo> mods =
                new List<ModInfo>();

            string[] files;

            try
            {
                files = Directory.GetFiles(
                    modsFolder,
                    "*.zip",
                    SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Błąd skanowania:\n" +
                    ex.Message;

                return;
            }

            foreach (string file in files)
            {
                FileInfo info =
                    new FileInfo(file);

                string fileStatus =
                    CheckZip(file);

                string type =
                    DetectModType(file);

                string source =
                    DetectSource(file);

                ModMetadata metadata =
                    DetectModMetadata(file);

                CompatibilityResult compatibility =
                    CheckCompatibility(
                        file,
                        fileStatus,
                        type);

                mods.Add(new ModInfo
                {
                    Name =
                        Path.GetFileNameWithoutExtension(file),

                    Source =
                        source,

                    Type =
                        type,

                    ModVersion =
                        metadata.Version,

                    RepositoryId =
                        metadata.RepositoryId,

                    ReleaseDate =
                        metadata.ReleaseDate,

                    UpdateStatus =
                        BuildUpdateStatus(
                            source,
                            metadata),

                    Size =
                        FormatSize(info.Length),

                    Modified =
                        info.LastWriteTime.ToString(
                            "dd.MM.yyyy HH:mm"),

                    Status =
                        fileStatus,

                    Compatibility =
                        compatibility.Status,

                    CompatibilityReason =
                        compatibility.Reason
                });
            }

            ModsGrid.ItemsSource =
                mods
                    .OrderBy(x => x.Name)
                    .ToList();

            StatusText.Text =
                "Wersja BeamNG: " +
                gameVersion +
                " | Znaleziono modów: " +
                mods.Count;
        }

        private string BuildUpdateStatus(
            string source,
            ModMetadata metadata)
        {
            if (source == "Repo BeamNG")
            {
                if (metadata.RepositoryId != "Nie podano")
                {
                    return "Gotowy do sprawdzenia";
                }

                return "Brak ID repo";
            }

            if (source == "Automation")
            {
                return "Brak źródła online";
            }

            return "Brak danych źródła";
        }

        private void InstallModButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(modsFolder))
            {
                MessageBox.Show(
                    "Najpierw wykryj BeamNG.",
                    "BeamNG Mod Manager");

                return;
            }

            OpenFileDialog dialog =
                new OpenFileDialog
                {
                    Title = "Wybierz mod BeamNG",
                    Filter = "Mody BeamNG (*.zip)|*.zip",
                    Multiselect = false
                };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string sourceFile =
                dialog.FileName;

            string zipStatus =
                CheckZip(sourceFile);

            if (zipStatus != "OK")
            {
                MessageBox.Show(
                    "Nie można zainstalować moda.\n\n" +
                    "Stan pliku: " +
                    zipStatus,
                    "Błąd instalacji",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            string modType =
                DetectModType(sourceFile);

            CompatibilityResult compatibility =
                CheckCompatibility(
                    sourceFile,
                    zipStatus,
                    modType);

            if (compatibility.Status ==
                "Ryzyko problemów")
            {
                MessageBoxResult answer =
                    MessageBox.Show(
                        "Program wykrył możliwy problem:\n\n" +
                        compatibility.Reason +
                        "\n\nCzy mimo to zainstalować mod?",
                        "Ostrzeżenie",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            if (compatibility.Status ==
                "Niezgodny")
            {
                MessageBox.Show(
                    "Ten mod został oznaczony jako niezgodny.\n\n" +
                    compatibility.Reason,
                    "Instalacja zablokowana",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            string destinationFile =
                Path.Combine(
                    modsFolder,
                    Path.GetFileName(sourceFile));

            try
            {
                if (File.Exists(destinationFile))
                {
                    MessageBoxResult answer =
                        MessageBox.Show(
                            "Mod o tej nazwie już istnieje.\n\n" +
                            "Czy go zastąpić?",
                            "Mod już istnieje",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (answer != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                File.Copy(
                    sourceFile,
                    destinationFile,
                    true);

                MessageBox.Show(
                    "Mod został zainstalowany.",
                    "Gotowe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ScanMods();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Nie udało się zainstalować moda.\n\n" +
                    ex.Message,
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string DetectSource(string file)
        {
            string path =
                file.Replace('\\', '/')
                    .ToLowerInvariant();

            if (path.Contains("/repo/"))
            {
                return "Repo BeamNG";
            }

            string name =
                Path.GetFileName(file)
                    .ToLowerInvariant();

            if (name.Contains("automation"))
            {
                return "Automation";
            }

            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Contains(
                        "automation",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return "Automation";
                    }
                }
            }
            catch
            {
            }

            return "Internet / ręczny";
        }

        private string DetectModType(string file)
        {
            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                bool vehicle = false;
                bool level = false;
                bool scenario = false;
                bool lua = false;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string path =
                        entry.FullName
                            .Replace('\\', '/')
                            .ToLowerInvariant();

                    if (path.StartsWith("vehicles/") ||
                        path.Contains("/vehicles/"))
                    {
                        vehicle = true;
                    }

                    if (path.StartsWith("levels/") ||
                        path.Contains("/levels/"))
                    {
                        level = true;
                    }

                    if (path.Contains("scenarios/"))
                    {
                        scenario = true;
                    }

                    if (path.EndsWith(".lua"))
                    {
                        lua = true;
                    }
                }

                if (vehicle && level)
                    return "Pojazd + mapa";

                if (vehicle)
                    return "Pojazd";

                if (level)
                    return "Mapa";

                if (scenario)
                    return "Scenariusz";

                if (lua)
                    return "Skrypt";

                return "Inny";
            }
            catch
            {
                return "Nieznany";
            }
        }

        private ModMetadata DetectModMetadata(string file)
        {
            ModMetadata metadata =
                new ModMetadata();

            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                List<ZipArchiveEntry> candidates =
                    archive.Entries
                        .Where(entry =>
                        {
                            string path =
                                entry.FullName
                                    .Replace('\\', '/')
                                    .ToLowerInvariant();

                            return path.EndsWith(".json") &&
                                   (
                                       path.Contains("mod_info") ||
                                       path.EndsWith("/info.json") ||
                                       path == "info.json" ||
                                       path.Contains("metadata")
                                   );
                        })
                        .OrderBy(entry =>
                            entry.FullName.Contains(
                                "mod_info",
                                StringComparison.OrdinalIgnoreCase)
                                ? 0
                                : 1)
                        .ToList();

                foreach (ZipArchiveEntry entry in candidates)
                {
                    string text;

                    try
                    {
                        using Stream stream =
                            entry.Open();

                        using StreamReader reader =
                            new StreamReader(stream);

                        text =
                            reader.ReadToEnd();
                    }
                    catch
                    {
                        continue;
                    }

                    if (metadata.Version == "Nie podano")
                    {
                        metadata.Version =
                            ExtractFirstValue(
                                text,
                                new[]
                                {
                                    "version_string",
                                    "versionString",
                                    "modVersion",
                                    "mod_version",
                                    "version"
                                });
                    }

                    if (metadata.RepositoryId == "Nie podano")
                    {
                        metadata.RepositoryId =
                            ExtractFirstValue(
                                text,
                                new[]
                                {
                                    "resource_id",
                                    "resourceId",
                                    "repository_id",
                                    "repositoryId"
                                });
                    }

                    if (metadata.ReleaseDate == "Nie podano")
                    {
                        metadata.ReleaseDate =
                            ExtractFirstValue(
                                text,
                                new[]
                                {
                                    "last_update",
                                    "lastUpdate",
                                    "updated",
                                    "updateDate",
                                    "releaseDate",
                                    "date"
                                });
                    }

                    if (metadata.Version != "Nie podano" &&
                        metadata.RepositoryId != "Nie podano" &&
                        metadata.ReleaseDate != "Nie podano")
                    {
                        break;
                    }
                }
            }
            catch
            {
                metadata.Version = "Błąd";
            }

            return metadata;
        }

        private string ExtractFirstValue(
            string text,
            IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                string escapedKey =
                    Regex.Escape(key);

                string[] patterns =
                {
                    $"\"{escapedKey}\"\\s*:\\s*\"([^\"]+)\"",
                    $"\"{escapedKey}\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)*)"
                };

                foreach (string pattern in patterns)
                {
                    Match match =
                        Regex.Match(
                            text,
                            pattern,
                            RegexOptions.IgnoreCase);

                    if (match.Success &&
                        match.Groups.Count > 1)
                    {
                        string value =
                            match.Groups[1]
                                .Value
                                .Trim();

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }
            }

            return "Nie podano";
        }

        private string CheckZip(string file)
        {
            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                if (archive.Entries.Count == 0)
                {
                    return "Pusty ZIP";
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    using Stream stream =
                        entry.Open();

                    byte[] buffer =
                        new byte[8192];

                    while (stream.Read(
                        buffer,
                        0,
                        buffer.Length) > 0)
                    {
                    }
                }

                return "OK";
            }
            catch
            {
                return "Uszkodzony";
            }
        }

        private CompatibilityResult CheckCompatibility(
            string file,
            string fileStatus,
            string modType)
        {
            if (fileStatus == "Uszkodzony")
            {
                return new CompatibilityResult
                {
                    Status = "Niezgodny",
                    Reason =
                        "Archiwum ZIP jest uszkodzone."
                };
            }

            if (fileStatus == "Pusty ZIP")
            {
                return new CompatibilityResult
                {
                    Status = "Niezgodny",
                    Reason =
                        "Archiwum ZIP jest puste."
                };
            }

            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                bool recognised = false;
                bool nestedZip = false;
                bool risky = false;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string path =
                        entry.FullName
                            .Replace('\\', '/')
                            .ToLowerInvariant();

                    if (path.StartsWith("vehicles/") ||
                        path.StartsWith("levels/") ||
                        path.StartsWith("art/") ||
                        path.StartsWith("lua/") ||
                        path.StartsWith("scripts/") ||
                        path.StartsWith("ui/") ||
                        path.StartsWith("gameplay/") ||
                        path.Contains("/vehicles/") ||
                        path.Contains("/levels/"))
                    {
                        recognised = true;
                    }

                    if (path.EndsWith(".zip"))
                    {
                        nestedZip = true;
                    }

                    if (path.EndsWith(".exe") ||
                        path.EndsWith(".bat") ||
                        path.EndsWith(".cmd") ||
                        path.EndsWith(".ps1"))
                    {
                        risky = true;
                    }
                }

                if (risky)
                {
                    return new CompatibilityResult
                    {
                        Status = "Ryzyko problemów",
                        Reason =
                            "Mod zawiera plik wykonywalny lub skrypt systemowy."
                    };
                }

                if (!recognised)
                {
                    return new CompatibilityResult
                    {
                        Status = "Ryzyko problemów",
                        Reason =
                            "Nie rozpoznano typowej struktury moda BeamNG."
                    };
                }

                if (nestedZip)
                {
                    return new CompatibilityResult
                    {
                        Status = "Prawdopodobnie zgodny",
                        Reason =
                            "Wewnątrz moda znajduje się dodatkowy ZIP."
                    };
                }

                if (modType == "Inny" ||
                    modType == "Nieznany")
                {
                    return new CompatibilityResult
                    {
                        Status = "Prawdopodobnie zgodny",
                        Reason =
                            "Archiwum jest sprawne, ale typ moda nie jest pewny."
                    };
                }

                return new CompatibilityResult
                {
                    Status = "Zgodny",
                    Reason =
                        "Archiwum i struktura moda wyglądają poprawnie."
                };
            }
            catch
            {
                return new CompatibilityResult
                {
                    Status = "Niezgodny",
                    Reason =
                        "Nie udało się przeanalizować moda."
                };
            }
        }

        private string FormatSize(long bytes)
        {
            double size = bytes;

            if (size >= 1024L * 1024 * 1024)
                return $"{size / (1024 * 1024 * 1024):0.00} GB";

            if (size >= 1024L * 1024)
                return $"{size / (1024 * 1024):0.00} MB";

            if (size >= 1024)
                return $"{size / 1024:0.00} KB";

            return $"{size} B";
        }
    }

    public class ModMetadata
    {
        public string Version { get; set; } = "Nie podano";
        public string RepositoryId { get; set; } = "Nie podano";
        public string ReleaseDate { get; set; } = "Nie podano";
    }

    public class CompatibilityResult
    {
        public string Status { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    public class ModInfo
    {
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public string Type { get; set; } = "";
        public string ModVersion { get; set; } = "";
        public string RepositoryId { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public string UpdateStatus { get; set; } = "";
        public string Size { get; set; } = "";
        public string Modified { get; set; } = "";
        public string Status { get; set; } = "";
        public string Compatibility { get; set; } = "";
        public string CompatibilityReason { get; set; } = "";
    }
}
