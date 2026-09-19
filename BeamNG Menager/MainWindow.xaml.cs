using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace BeamNGModManager
{
    public partial class MainWindow : Window
    {
        private string? modsFolder;
        private string gameVersion = "Nieznana";
        private List<ModInfo> currentMods = new List<ModInfo>();

        private static readonly HttpClient httpClient = CreateHttpClient();

        public MainWindow()
        {
            InitializeComponent();

            // BeamNG jest wykrywany automatycznie przy uruchomieniu programu.
            // Przycisk w panelu służy później tylko do ręcznego ponownego sprawdzenia.
            Loaded += (_, _) =>
                DetectBeamNg(true);
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BeamNGModManager/0.1");

            return client;
        }

        private void DetectButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DetectBeamNg(false);
        }

        private void DetectBeamNg(bool scanModsAfterDetection)
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

            string? executableVersion =
                DetectGameVersionFromExecutable();

            if (!string.IsNullOrWhiteSpace(executableVersion))
            {
                gameVersion = executableVersion;
            }

            if (!Directory.Exists(userFolder))
            {
                StatusText.Text =
                    "Nie znaleziono BeamNG lub folderu użytkownika.\n\n" +
                    userFolder;

                ScanModsButton.IsEnabled = false;
                InstallModButton.IsEnabled = false;
                CheckUpdatesButton.IsEnabled = false;
                DownloadModsButton.IsEnabled = false;
                return;
            }

            modsFolder =
                Path.Combine(userFolder, "mods");

            if (!Directory.Exists(modsFolder))
            {
                Directory.CreateDirectory(modsFolder);
            }

            ScanModsButton.IsEnabled = true;
            InstallModButton.IsEnabled = true;
            CheckUpdatesButton.IsEnabled = true;
            DownloadModsButton.IsEnabled = true;

            if (scanModsAfterDetection)
            {
                ScanMods();
                return;
            }

            StatusText.Text =
                "BeamNG wykryty poprawnie. Wersja: " +
                gameVersion +
                " | Folder modów: " +
                modsFolder;
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
                    "Nie wykryto BeamNG. Użyj „Sprawdź BeamNG” w sekcji Narzędzia.";

                return;
            }

            Dictionary<string, string> previousUpdateStatuses =
                currentMods
                    .Where(mod =>
                        !string.IsNullOrWhiteSpace(mod.FilePath))
                    .GroupBy(
                        mod => mod.FilePath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First().UpdateStatus,
                        StringComparer.OrdinalIgnoreCase);

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

                ModMetadata metadata =
                    DetectModMetadata(file);

                string source =
                    DetectSource(
                        file,
                        metadata);

                CompatibilityResult compatibility =
                    CheckCompatibility(
                        file,
                        fileStatus,
                        type);

                string updateStatus =
                    BuildUpdateStatus(
                        source,
                        metadata);

                if (previousUpdateStatuses.TryGetValue(
                    file,
                    out string? previousUpdateStatus) &&
                    !string.IsNullOrWhiteSpace(previousUpdateStatus))
                {
                    updateStatus =
                        previousUpdateStatus;
                }

                mods.Add(new ModInfo
                {
                    Name =
                        Path.GetFileNameWithoutExtension(file),

                    FilePath =
                        file,

                    Source =
                        source,

                    Type =
                        type,

                    ModVersion =
                        metadata.Version,

                    RepositoryId =
                        metadata.RepositoryId,

                    RepositoryTitle =
                        metadata.Title,

                    ReleaseDate =
                        metadata.ReleaseDate,

                    UpdateStatus =
                        updateStatus,

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

            AnalyzeConflicts(mods);

            currentMods =
                mods
                    .OrderBy(x => x.Name)
                    .ToList();

            RefreshGrid();

            StatusText.Text =
                "Biblioteka odświeżona. Wersja BeamNG: " +
                gameVersion +
                " | Znaleziono modów: " +
                currentMods.Count;
        }

        private void AnalyzeConflicts(List<ModInfo> mods)
        {
            Dictionary<string, List<ModInfo>> ownersByPath =
                new Dictionary<string, List<ModInfo>>(
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<ModInfo, int> conflictCounts =
                mods.ToDictionary(
                    mod => mod,
                    mod => 0);

            Dictionary<ModInfo, HashSet<string>> conflictingMods =
                mods.ToDictionary(
                    mod => mod,
                    mod => new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase));

            foreach (ModInfo mod in mods)
            {
                mod.ConflictStatus = "Brak";
                mod.ConflictDetails =
                    "Nie wykryto plików nadpisywanych przez inne mody.";
                mod.ConflictColor = "#237A4B";

                if (string.IsNullOrWhiteSpace(mod.FilePath) ||
                    !File.Exists(mod.FilePath))
                {
                    continue;
                }

                foreach (string path in GetConflictRelevantPaths(mod.FilePath))
                {
                    if (!ownersByPath.TryGetValue(
                        path,
                        out List<ModInfo>? owners))
                    {
                        owners = new List<ModInfo>();
                        ownersByPath[path] = owners;
                    }

                    owners.Add(mod);
                }
            }

            foreach (KeyValuePair<string, List<ModInfo>> item in ownersByPath)
            {
                List<ModInfo> owners =
                    item.Value
                        .Distinct()
                        .ToList();

                if (owners.Count < 2)
                {
                    continue;
                }

                foreach (ModInfo mod in owners)
                {
                    conflictCounts[mod]++;

                    foreach (ModInfo other in owners)
                    {
                        if (!ReferenceEquals(mod, other))
                        {
                            conflictingMods[mod].Add(other.Name);
                        }
                    }
                }
            }

            foreach (ModInfo mod in mods)
            {
                int sharedFiles =
                    conflictCounts[mod];

                int otherMods =
                    conflictingMods[mod].Count;

                if (sharedFiles == 0)
                {
                    continue;
                }

                mod.ConflictStatus =
                    sharedFiles +
                    " plików / " +
                    otherMods +
                    " modów";

                List<string> names =
                    conflictingMods[mod]
                        .OrderBy(name => name)
                        .Take(5)
                        .ToList();

                string details =
                    "Wspólne pliki z: " +
                    string.Join(", ", names);

                if (otherMods > names.Count)
                {
                    details +=
                        " i " +
                        (otherMods - names.Count) +
                        " innymi.";
                }
                else
                {
                    details += ".";
                }

                mod.ConflictDetails = details;

                mod.ConflictColor =
                    sharedFiles >= 10
                        ? "#A53A43"
                        : "#B85C1E";
            }
        }

        private HashSet<string> GetConflictRelevantPaths(
            string file)
        {
            HashSet<string> paths =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    string? path =
                        NormalizeConflictPath(
                            entry.FullName);

                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(path);
                    }
                }
            }
            catch
            {
            }

            return paths;
        }

        private string? NormalizeConflictPath(
            string rawPath)
        {
            string path =
                rawPath
                    .Replace('\\', '/')
                    .TrimStart('/')
                    .ToLowerInvariant();

            string[] roots =
            {
                "vehicles/",
                "levels/",
                "art/",
                "lua/",
                "scripts/",
                "ui/",
                "gameplay/",
                "settings/",
                "missions/"
            };

            int bestIndex = -1;

            foreach (string root in roots)
            {
                int index =
                    path.IndexOf(
                        root,
                        StringComparison.OrdinalIgnoreCase);

                if (index >= 0 &&
                    (bestIndex < 0 || index < bestIndex))
                {
                    bestIndex = index;
                }
            }

            if (bestIndex < 0)
            {
                return null;
            }

            string normalized =
                path.Substring(bestIndex);

            if (normalized.EndsWith("/"))
            {
                return null;
            }

            return normalized;
        }

        private void RefreshGrid()
        {
            ModsGrid.ItemsSource = null;
            ModsGrid.ItemsSource = currentMods;
        }

        private string BuildUpdateStatus(
            string source,
            ModMetadata metadata)
        {
            if (source == "Repo BeamNG")
            {
                if (metadata.RepositoryId != "Nie podano")
                {
                    return "Nie sprawdzono";
                }

                return "Brak danych repo";
            }

            if (source == "Automation")
            {
                return "Brak źródła online";
            }

            return "Brak danych źródła";
        }

        private async void CheckUpdatesButton_Click(
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

            if (currentMods.Count == 0)
            {
                ScanMods();
            }

            List<ModInfo> repoMods =
                currentMods
                    .Where(mod =>
                        mod.Source == "Repo BeamNG")
                    .ToList();

            if (repoMods.Count == 0)
            {
                StatusText.Text =
                    "Nie znaleziono modów z Repo BeamNG do sprawdzenia.";

                return;
            }

            CheckUpdatesButton.IsEnabled = false;
            ScanModsButton.IsEnabled = false;
            InstallModButton.IsEnabled = false;

            int checkedCount = 0;
            int updatesFound = 0;

            try
            {
                foreach (ModInfo mod in repoMods)
                {
                    StatusText.Text =
                        "Sprawdzanie aktualizacji: " +
                        (checkedCount + 1) +
                        "/" +
                        repoMods.Count +
                        " — " +
                        mod.Name;

                    string result =
                        await CheckBeamNgUpdateAsync(mod);

                    mod.UpdateStatus = result;

                    if (result.StartsWith(
                        "Aktualizacja:",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        updatesFound++;
                    }

                    checkedCount++;
                    RefreshGrid();
                }

                StatusText.Text =
                    "Sprawdzono aktualizacje Repo BeamNG: " +
                    checkedCount +
                    ". Dostępne aktualizacje: " +
                    updatesFound +
                    ".";
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                ScanModsButton.IsEnabled = true;
                InstallModButton.IsEnabled = true;
            }
        }

        private async Task<string> CheckBeamNgUpdateAsync(
            ModInfo mod)
        {
            if (string.IsNullOrWhiteSpace(mod.RepositoryId) ||
                mod.RepositoryId == "Nie podano")
            {
                return "Brak danych repo";
            }

            string title =
                string.IsNullOrWhiteSpace(mod.RepositoryTitle) ||
                mod.RepositoryTitle == "Nie podano"
                    ? mod.Name
                    : mod.RepositoryTitle;

            string url =
                BuildBeamNgResourceUrl(
                    title,
                    mod.RepositoryId);

            try
            {
                using HttpResponseMessage response =
                    await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return "Błąd HTTP " +
                        (int)response.StatusCode;
                }

                string html =
                    await response.Content.ReadAsStringAsync();

                string? remoteVersion =
                    ExtractRemoteVersion(html);

                if (string.IsNullOrWhiteSpace(remoteVersion))
                {
                    return "Nie odczytano wersji";
                }

                if (string.IsNullOrWhiteSpace(mod.ModVersion) ||
                    mod.ModVersion == "Nie podano" ||
                    mod.ModVersion == "Błąd")
                {
                    return "Online: " + remoteVersion;
                }

                int comparison =
                    CompareVersionStrings(
                        mod.ModVersion,
                        remoteVersion);

                if (comparison < 0)
                {
                    return "Aktualizacja: " +
                        remoteVersion;
                }

                if (comparison == 0)
                {
                    return "Aktualny";
                }

                return "Lokalna nowsza";
            }
            catch (TaskCanceledException)
            {
                return "Przekroczono czas";
            }
            catch
            {
                return "Błąd połączenia";
            }
        }

        private string BuildBeamNgResourceUrl(
            string title,
            string repositoryId)
        {
            string slug =
                title
                    .ToLowerInvariant()
                    .Normalize();

            slug =
                Regex.Replace(
                    slug,
                    @"[^a-z0-9]+",
                    "-")
                    .Trim('-');

            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = "resource";
            }

            return
                "https://www.beamng.com/resources/" +
                slug +
                "." +
                repositoryId +
                "/";
        }

        private string? ExtractRemoteVersion(
            string html)
        {
            string cleaned =
                Regex.Replace(
                    html,
                    @"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>",
                    " ",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            cleaned =
                Regex.Replace(
                    cleaned,
                    @"<style\b[^<]*(?:(?!</style>)<[^<]*)*</style>",
                    " ",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            cleaned =
                Regex.Replace(
                    cleaned,
                    @"<[^>]+>",
                    " ");

            cleaned =
                WebUtility.HtmlDecode(cleaned);

            cleaned =
                Regex.Replace(
                    cleaned,
                    @"\s+",
                    " ");

            Match match =
                Regex.Match(
                    cleaned,
                    @"\bVersion\s+([0-9][0-9A-Za-z._+\-]*)",
                    RegexOptions.IgnoreCase);

            if (match.Success &&
                match.Groups.Count > 1)
            {
                return match.Groups[1]
                    .Value
                    .Trim();
            }

            return null;
        }

        private int CompareVersionStrings(
            string localVersion,
            string remoteVersion)
        {
            int[] localNumbers =
                Regex.Matches(
                    localVersion,
                    @"\d+")
                    .Select(match =>
                        int.TryParse(
                            match.Value,
                            out int value)
                            ? value
                            : 0)
                    .ToArray();

            int[] remoteNumbers =
                Regex.Matches(
                    remoteVersion,
                    @"\d+")
                    .Select(match =>
                        int.TryParse(
                            match.Value,
                            out int value)
                            ? value
                            : 0)
                    .ToArray();

            if (localNumbers.Length == 0 ||
                remoteNumbers.Length == 0)
            {
                return string.Compare(
                    localVersion,
                    remoteVersion,
                    StringComparison.OrdinalIgnoreCase);
            }

            int max =
                Math.Max(
                    localNumbers.Length,
                    remoteNumbers.Length);

            for (int i = 0; i < max; i++)
            {
                int local =
                    i < localNumbers.Length
                        ? localNumbers[i]
                        : 0;

                int remote =
                    i < remoteNumbers.Length
                        ? remoteNumbers[i]
                        : 0;

                if (local < remote)
                {
                    return -1;
                }

                if (local > remote)
                {
                    return 1;
                }
            }

            return 0;
        }

        private void DownloadModsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(modsFolder) ||
                !Directory.Exists(modsFolder))
            {
                MessageBox.Show(
                    "Nie wykryto folderu modów BeamNG.",
                    "BeamNG Mod Manager",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            DownloadWindow window =
                new DownloadWindow(modsFolder)
                {
                    Owner = this
                };

            bool? result =
                window.ShowDialog();

            if (result == true)
            {
                ScanMods();
            }
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

        private string DetectSource(
            string file,
            ModMetadata metadata)
        {
            string path =
                file.Replace('\\', '/')
                    .ToLowerInvariant();

            if (path.Contains("/repo/") ||
                metadata.RepositoryId != "Nie podano")
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

                    if (metadata.Title == "Nie podano")
                    {
                        metadata.Title =
                            ExtractFirstValue(
                                text,
                                new[]
                                {
                                    "title",
                                    "name"
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
                                    "release_date",
                                    "date"
                                });
                    }

                    if (metadata.Version != "Nie podano" &&
                        metadata.RepositoryId != "Nie podano" &&
                        metadata.Title != "Nie podano")
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
        public string Title { get; set; } = "Nie podano";
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
        public string FilePath { get; set; } = "";
        public string Source { get; set; } = "";
        public string Type { get; set; } = "";
        public string ModVersion { get; set; } = "";
        public string RepositoryId { get; set; } = "";
        public string RepositoryTitle { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public string UpdateStatus { get; set; } = "";
        public string Size { get; set; } = "";
        public string Modified { get; set; } = "";
        public string Status { get; set; } = "";
        public string ConflictStatus { get; set; } = "Brak";
        public string ConflictDetails { get; set; } = "";
        public string ConflictColor { get; set; } = "#4C5563";
        public string Compatibility { get; set; } = "";
        public string CompatibilityReason { get; set; } = "";
    }
}
