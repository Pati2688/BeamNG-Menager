using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BeamNGModManager
{
    public partial class MainWindow : Window
    {
        private string? modsFolder;
        private string gameVersion = "Nieznana";
        private List<ModInfo> currentMods = new List<ModInfo>();
        private List<CatalogMod> allCatalogMods = new List<CatalogMod>();
        private List<CatalogMod> catalogMods = new List<CatalogMod>();

        private static readonly HttpClient httpClient = CreateHttpClient();
        private static readonly HttpClient downloadClient = CreateDownloadHttpClient();

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

        private static HttpClient CreateDownloadHttpClient()
        {
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BeamNGModManager/0.3");

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

        private void LibraryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DownloadView.Visibility =
                Visibility.Collapsed;

            LibraryView.Visibility =
                Visibility.Visible;

            DetailsPanel.Visibility =
                Visibility.Visible;

            LibraryButton.Background =
                (System.Windows.Media.Brush)
                FindResource("AccentSoftBrush");

            DownloadModsButton.Background =
                System.Windows.Media.Brushes.Transparent;
        }

        private async void DownloadModsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LibraryView.Visibility =
                Visibility.Collapsed;

            DetailsPanel.Visibility =
                Visibility.Collapsed;

            DownloadView.Visibility =
                Visibility.Visible;

            LibraryButton.Background =
                System.Windows.Media.Brushes.Transparent;

            DownloadModsButton.Background =
                (System.Windows.Media.Brush)
                FindResource("AccentSoftBrush");

            if (catalogMods.Count == 0)
            {
                await LoadCatalogAsync();
            }
        }

        private async void CatalogTile_Click(
            object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.Border border ||
                border.DataContext is not CatalogMod mod)
            {
                return;
            }

            e.Handled = true;

            CatalogBrowseView.Visibility =
                Visibility.Collapsed;

            CatalogDetailView.Visibility =
                Visibility.Visible;

            CatalogDetailView.DataContext =
                mod;

            if (!mod.DetailsLoaded)
            {
                CatalogStatusText.Text =
                    "Wczytywanie szczegółów: " +
                    mod.Title +
                    "...";

                try
                {
                    await LoadBeamNgModDetailsAsync(
                        mod);
                }
                catch
                {
                    // Zostawiamy dane z katalogu, jeśli strona
                    // szczegółów chwilowo nie odpowiada.
                }

                CatalogDetailView.DataContext = null;
                CatalogDetailView.DataContext = mod;
            }

            CatalogStatusText.Text =
                "Szczegóły: " +
                mod.Title;
        }

        private void CatalogDetailBackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            CatalogDetailView.Visibility =
                Visibility.Collapsed;

            CatalogBrowseView.Visibility =
                Visibility.Visible;

            CatalogStatusText.Text =
                "Gotowe: " +
                catalogMods.Count +
                " modów z Repo BeamNG.";
        }

        private async void RefreshCatalogButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await LoadCatalogAsync();
        }

        private async Task LoadCatalogAsync()
        {
            RefreshCatalogButton.IsEnabled = false;
            DownloadModsButton.IsEnabled = false;

            CatalogStatusText.Text =
                "Pobieranie najnowszych modów z Repo BeamNG...";

            try
            {
                catalogMods =
                    await LoadBeamNgCatalogAsync();

                CatalogTiles.ItemsSource = null;
                CatalogTiles.ItemsSource = catalogMods;

                CatalogStatusText.Text =
                    "Wczytywanie miniaturek...";

                await CacheCatalogThumbnailsAsync(
                    catalogMods);

                CatalogTiles.ItemsSource = null;
                CatalogTiles.ItemsSource = catalogMods;

                CatalogStatusText.Text =
                    "Gotowe: " +
                    catalogMods.Count +
                    " modów z Repo BeamNG.";
            }
            catch (Exception ex)
            {
                CatalogStatusText.Text =
                    "Nie udało się wczytać Repo BeamNG: " +
                    ex.Message;
            }
            finally
            {
                RefreshCatalogButton.IsEnabled = true;
                DownloadModsButton.IsEnabled = true;
            }
        }

        private async Task<List<CatalogMod>> LoadBeamNgCatalogAsync()
        {
            const string pageUrl =
                "https://www.beamng.com/resources/?order=resource_date";

            string html =
                await httpClient.GetStringAsync(
                    pageUrl);

            Uri pageUri =
                new Uri(pageUrl);

            List<CatalogMod> result =
                new List<CatalogMod>();

            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            List<string> cards =
                ExtractBeamNgResourceCards(
                    html);

            foreach (string card in cards)
            {
                CatalogMod? mod =
                    ParseBeamNgResourceCard(
                        card,
                        pageUri);

                if (mod == null ||
                    !seen.Add(mod.ResourceUrl))
                {
                    continue;
                }

                result.Add(mod);

                if (result.Count >= 24)
                {
                    break;
                }
            }

            if (result.Count == 0)
            {
                // Awaryjny parser dla sytuacji, gdy BeamNG zmieni
                // opakowanie listy, ale zachowa linki do zasobów.
                string listingHtml =
                    GetBeamNgListingRegion(
                        html);

                Regex linkRegex =
                    new Regex(
                        "href=[\\\"'](?<url>(?:https://www\\.beamng\\.com)?/?resources/(?<slug>[^/\\\"'#?]+\\.[0-9]+)/?)[\\\"'][^>]*>(?<title>.*?)</a>",
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                foreach (Match match in linkRegex.Matches(
                    listingHtml))
                {
                    string title =
                        StripHtml(
                            match.Groups["title"].Value);

                    if (!IsValidResourceTitle(title))
                    {
                        continue;
                    }

                    string absoluteUrl =
                        MakeAbsoluteUrl(
                            WebUtility.HtmlDecode(
                                match.Groups["url"].Value),
                            pageUri);

                    if (!seen.Add(absoluteUrl))
                    {
                        continue;
                    }

                    string nearby =
                        GetNearbyHtml(
                            listingHtml,
                            match.Index,
                            2600);

                    result.Add(
                        new CatalogMod
                        {
                            Title = title,
                            Source = "Repo BeamNG",
                            Author =
                                ExtractBeamNgAuthor(
                                    nearby),
                            Category =
                                ExtractBeamNgCategory(
                                    nearby),
                            Version =
                                ExtractBeamNgCardVersion(
                                    nearby),
                            Description =
                                ExtractBeamNgCardDescription(
                                    nearby),
                            ThumbnailUrl =
                                ExtractThumbnailUrl(
                                    nearby,
                                    pageUri),
                            ResourceUrl =
                                absoluteUrl
                        });

                    if (result.Count >= 24)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private List<string> ExtractBeamNgResourceCards(
            string html)
        {
            string[] patterns =
            {
                "<li\\b[^>]*class=[\\\"'][^\\\"']*resourceListItem[^\\\"']*[\\\"'][^>]*>(?<card>.*?)</li>",
                "<article\\b[^>]*class=[\\\"'][^\\\"']*(?:resource|structItem)[^\\\"']*[\\\"'][^>]*>(?<card>.*?)</article>"
            };

            foreach (string pattern in patterns)
            {
                MatchCollection matches =
                    Regex.Matches(
                        html,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                if (matches.Count == 0)
                {
                    continue;
                }

                return matches
                    .Cast<Match>()
                    .Select(match =>
                        match.Value)
                    .ToList();
            }

            return new List<string>();
        }

        private CatalogMod? ParseBeamNgResourceCard(
            string card,
            Uri pageUri)
        {
            Regex linkRegex =
                new Regex(
                    "href=[\\\"'](?<url>(?:https://www\\.beamng\\.com)?/?resources/(?<slug>[^/\\\"'#?]+\\.[0-9]+)/?)[\\\"'][^>]*>(?<title>.*?)</a>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            MatchCollection matches =
                linkRegex.Matches(card);

            Match? resourceMatch = null;
            string title = "";

            foreach (Match match in matches)
            {
                string candidate =
                    StripHtml(
                        match.Groups["title"].Value);

                if (!IsValidResourceTitle(candidate))
                {
                    continue;
                }

                resourceMatch = match;
                title = candidate;
                break;
            }

            if (resourceMatch == null)
            {
                return null;
            }

            string resourceUrl =
                MakeAbsoluteUrl(
                    WebUtility.HtmlDecode(
                        resourceMatch.Groups["url"].Value),
                    pageUri);

            return new CatalogMod
            {
                Title = title,
                Source = "Repo BeamNG",
                Author =
                    ExtractBeamNgAuthor(
                        card),
                Category =
                    ExtractBeamNgCategory(
                        card),
                Version =
                    ExtractBeamNgCardVersion(
                        card),
                Description =
                    ExtractBeamNgCardDescription(
                        card),
                ThumbnailUrl =
                    ExtractThumbnailUrl(
                        card,
                        pageUri),
                ResourceUrl =
                    resourceUrl
            };
        }

        private bool IsValidResourceTitle(
            string title)
        {
            if (string.IsNullOrWhiteSpace(title) ||
                title.Length < 2)
            {
                return false;
            }

            string[] invalid =
            {
                "Vehicles",
                "Scenarios",
                "Terrains, Levels, Maps",
                "User Interface Apps",
                "Sounds",
                "License Plates",
                "Track Builder",
                "Mods of Mods",
                "Skins",
                "Automation",
                "Image",
                "Download Now"
            };

            return !invalid.Any(value =>
                title.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase));
        }

        private string GetBeamNgListingRegion(
            string html)
        {
            Match listMatch =
                Regex.Match(
                    html,
                    "<ol\\b[^>]*class=[\\\"'][^\\\"']*resourceList[^\\\"']*[\\\"'][^>]*>",
                    RegexOptions.IgnoreCase);

            if (listMatch.Success)
            {
                return html.Substring(
                    listMatch.Index);
            }

            int marker =
                html.LastIndexOf(
                    "order=resource_date",
                    StringComparison.OrdinalIgnoreCase);

            return marker >= 0
                ? html.Substring(marker)
                : html;
        }

        private string ExtractBeamNgAuthor(
            string html)
        {
            string author =
                ExtractFirstText(
                    html,
                    "class=[\\\"'][^\\\"']*(?:username|username--style)[^\\\"']*[\\\"'][^>]*>(?<text>.*?)</a>");

            if (!string.IsNullOrWhiteSpace(author))
            {
                return author;
            }

            author =
                ExtractFirstText(
                    html,
                    "href=[\\\"'][^\\\"']*/members/[^\\\"']+[\\\"'][^>]*>(?<text>.*?)</a>");

            return string.IsNullOrWhiteSpace(author)
                ? "—"
                : author;
        }

        private string ExtractBeamNgCategory(
            string html)
        {
            string category =
                ExtractFirstText(
                    html,
                    "href=[\\\"'][^\\\"']*/resources/categories/[^\\\"']+[\\\"'][^>]*>(?<text>.*?)</a>");

            return string.IsNullOrWhiteSpace(category)
                ? "Repo"
                : category;
        }

        private string ExtractBeamNgCardVersion(
            string html)
        {
            string version =
                ExtractFirstText(
                    html,
                    "<(?:span|div)[^>]*class=[\\\"'][^\\\"']*version[^\\\"']*[\\\"'][^>]*>(?<text>.*?)</(?:span|div)>");

            return string.IsNullOrWhiteSpace(version)
                ? "—"
                : version;
        }

        private string ExtractBeamNgCardDescription(
            string html)
        {
            string[] patterns =
            {
                "<(?:div|p)[^>]*class=[\\\"'][^\\\"']*(?:tagLine|resourceTagLine|resourceDescription)[^\\\"']*[\\\"'][^>]*>(?<text>.*?)</(?:div|p)>",
                "<p[^>]*>(?<text>.*?)</p>"
            };

            foreach (string pattern in patterns)
            {
                string description =
                    ExtractFirstText(
                        html,
                        pattern);

                if (!string.IsNullOrWhiteSpace(description) &&
                    description.Length >= 3)
                {
                    return description;
                }
            }

            return "";
        }

        private async Task CacheCatalogThumbnailsAsync(
            List<CatalogMod> mods)
        {
            using SemaphoreSlim semaphore =
                new SemaphoreSlim(6);

            IEnumerable<Task> tasks =
                mods.Select(
                    async mod =>
                    {
                        await semaphore.WaitAsync();

                        try
                        {
                            await EnsureCatalogThumbnailAsync(
                                mod);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

            await Task.WhenAll(tasks);
        }

        private async Task EnsureCatalogThumbnailAsync(
            CatalogMod mod)
        {
            string imageUrl =
                mod.ThumbnailUrl;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                try
                {
                    string html =
                        await httpClient.GetStringAsync(
                            mod.ResourceUrl);

                    Uri resourceUri =
                        new Uri(mod.ResourceUrl);

                    imageUrl =
                        ExtractThumbnailUrl(
                            html,
                            resourceUri);

                    if (string.IsNullOrWhiteSpace(imageUrl))
                    {
                        imageUrl =
                            ExtractGalleryImageUrls(
                                html,
                                resourceUri)
                                .FirstOrDefault() ??
                            "";
                    }
                }
                catch
                {
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            string cached =
                await CacheRemoteImageAsync(
                    imageUrl,
                    mod.ResourceUrl,
                    "thumbs");

            if (!string.IsNullOrWhiteSpace(cached))
            {
                mod.ThumbnailUrl =
                    cached;
            }
        }

        private async Task LoadBeamNgModDetailsAsync(
            CatalogMod mod)
        {
            string html =
                await httpClient.GetStringAsync(
                    mod.ResourceUrl);

            Uri resourceUri =
                new Uri(mod.ResourceUrl);

            string description =
                ExtractResourceDescription(
                    html);

            if (!string.IsNullOrWhiteSpace(description))
            {
                mod.Description =
                    description;
            }

            string author =
                ExtractBeamNgAuthor(
                    html);

            if (!string.IsNullOrWhiteSpace(author) &&
                author != "—")
            {
                mod.Author =
                    author;
            }

            string category =
                ExtractBeamNgCategory(
                    html);

            if (!string.IsNullOrWhiteSpace(category) &&
                category != "Repo")
            {
                mod.Category =
                    category;
            }

            string version =
                ExtractResourceVersion(
                    html);

            if (!string.IsNullOrWhiteSpace(version))
            {
                mod.Version =
                    version;
            }

            string plain =
                StripHtml(html);

            mod.FileSize =
                ExtractRegexValue(
                    plain,
                    @"Download Now\\s+(?<value>[0-9][0-9.,]*\\s*(?:KB|MB|GB))");

            mod.TotalDownloads =
                ExtractRegexValue(
                    plain,
                    @"Total Downloads:\\s*(?<value>[0-9][0-9,.\\s]*)");

            mod.Subscriptions =
                ExtractRegexValue(
                    plain,
                    @"Subscriptions:\\s*(?<value>[0-9][0-9,.\\s]*)");

            mod.FirstRelease =
                ExtractBetweenLabels(
                    plain,
                    "First Release:",
                    "Last Update:");

            mod.LastUpdate =
                ExtractBetweenLabels(
                    plain,
                    "Last Update:",
                    "Category:");

            mod.Rating =
                ExtractBetweenLabels(
                    plain,
                    "All-Time Rating:",
                    "Version ");

            string detailThumbnail =
                ExtractThumbnailUrl(
                    html,
                    resourceUri);

            if (!string.IsNullOrWhiteSpace(
                detailThumbnail))
            {
                string cachedThumbnail =
                    await CacheRemoteImageAsync(
                        detailThumbnail,
                        mod.ResourceUrl,
                        "thumbs");

                if (!string.IsNullOrWhiteSpace(
                    cachedThumbnail))
                {
                    mod.ThumbnailUrl =
                        cachedThumbnail;
                }
            }

            List<string> galleryUrls =
                ExtractGalleryImageUrls(
                    html,
                    resourceUri);

            mod.GalleryImages =
                await CacheGalleryImagesAsync(
                    galleryUrls,
                    mod.ResourceUrl);

            mod.DetailsLoaded = true;
        }

        private string ExtractMetaContent(
            string html,
            string propertyName)
        {
            string escaped =
                Regex.Escape(
                    propertyName);

            string[] patterns =
            {
                "<meta[^>]+(?:property|name)=[\\\"']" +
                escaped +
                "[\\\"'][^>]+content=[\\\"'](?<value>[^\\\"']+)[\\\"'][^>]*>",

                "<meta[^>]+content=[\\\"'](?<value>[^\\\"']+)[\\\"'][^>]+(?:property|name)=[\\\"']" +
                escaped +
                "[\\\"'][^>]*>"
            };

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        html,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                if (match.Success)
                {
                    return WebUtility.HtmlDecode(
                        match.Groups["value"].Value)
                        .Trim();
                }
            }

            return "";
        }

        private string ExtractRegexValue(
            string text,
            string pattern)
        {
            Match match =
                Regex.Match(
                    text,
                    pattern,
                    RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return "—";
            }

            string value =
                match.Groups["value"]
                    .Value
                    .Trim();

            return string.IsNullOrWhiteSpace(value)
                ? "—"
                : value;
        }

        private string ExtractBetweenLabels(
            string text,
            string startLabel,
            string endLabel)
        {
            Match match =
                Regex.Match(
                    text,
                    Regex.Escape(startLabel) +
                    @"\\s*(?<value>.*?)\\s*" +
                    Regex.Escape(endLabel),
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (!match.Success)
            {
                return "—";
            }

            string value =
                Regex.Replace(
                    match.Groups["value"].Value,
                    @"\\s+",
                    " ")
                    .Trim();

            return string.IsNullOrWhiteSpace(value)
                ? "—"
                : value;
        }

        private async Task<List<string>> CacheGalleryImagesAsync(
            List<string> imageUrls,
            string resourceUrl)
        {
            List<string> selected =
                imageUrls
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .ToList();

            using SemaphoreSlim semaphore =
                new SemaphoreSlim(4);

            Task<string>[] tasks =
                selected
                    .Select(
                        async imageUrl =>
                        {
                            await semaphore.WaitAsync();

                            try
                            {
                                return await CacheRemoteImageAsync(
                                    imageUrl,
                                    resourceUrl,
                                    "gallery");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        })
                    .ToArray();

            string[] cached =
                await Task.WhenAll(tasks);

            return cached
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .ToList();
        }

        private async Task<string> CacheRemoteImageAsync(
            string imageUrl,
            string referrerUrl,
            string cacheFolder)
        {
            try
            {
                Uri imageUri =
                    new Uri(imageUrl);

                string hash =
                    Convert.ToHexString(
                        SHA256.HashData(
                            Encoding.UTF8.GetBytes(
                                imageUrl)))
                    .ToLowerInvariant();

                string cacheDirectory =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "BeamNGModManager",
                        "Cache",
                        cacheFolder);

                Directory.CreateDirectory(
                    cacheDirectory);

                string[] existing =
                    Directory.GetFiles(
                        cacheDirectory,
                        hash + ".*");

                if (existing.Length > 0 &&
                    new FileInfo(existing[0]).Length > 0)
                {
                    return new Uri(
                        existing[0])
                        .AbsoluteUri;
                }

                using HttpRequestMessage request =
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        imageUri);

                if (Uri.TryCreate(
                    referrerUrl,
                    UriKind.Absolute,
                    out Uri? referrer))
                {
                    request.Headers.Referrer =
                        referrer;
                }

                using HttpResponseMessage response =
                    await httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                byte[] bytes =
                    await response.Content
                        .ReadAsByteArrayAsync();

                if (bytes.Length == 0)
                {
                    return "";
                }

                string extension =
                    Path.GetExtension(
                        imageUri.AbsolutePath)
                        .ToLowerInvariant();

                string? mediaType =
                    response.Content.Headers
                        .ContentType?
                        .MediaType;

                if (mediaType ==
                    "image/jpeg")
                {
                    extension = ".jpg";
                }
                else if (mediaType ==
                    "image/png")
                {
                    extension = ".png";
                }
                else if (mediaType ==
                    "image/gif")
                {
                    extension = ".gif";
                }
                else if (mediaType ==
                    "image/webp")
                {
                    extension = ".webp";
                }

                if (extension != ".jpg" &&
                    extension != ".jpeg" &&
                    extension != ".png" &&
                    extension != ".gif" &&
                    extension != ".webp")
                {
                    extension = ".jpg";
                }

                string cacheFile =
                    Path.Combine(
                        cacheDirectory,
                        hash +
                        extension);

                await File.WriteAllBytesAsync(
                    cacheFile,
                    bytes);

                return new Uri(
                    cacheFile)
                    .AbsoluteUri;
            }
            catch
            {
                return "";
            }
        }

        private string MakeAbsoluteUrl(
            string value,
            Uri baseUri)
        {
            if (Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? absoluteUri))
            {
                return absoluteUri.ToString();
            }

            return new Uri(
                baseUri,
                value)
                .ToString();
        }

        private string ExtractResourceDescription(
            string html)
        {
            string[] bodyPatterns =
            {
                "<div[^>]*class=[\\\"'][^\\\"']*bbWrapper[^\\\"']*[\\\"'][^>]*>(?<text>.*?)(?=<div[^>]*class=[\\\"'][^\\\"']*(?:resourceUpdate|resourceReview|message-attribution)[^\\\"']*[\\\"']|<h3[^>]*>Recent Reviews|$)",
                "<blockquote[^>]*class=[\\\"'][^\\\"']*(?:messageText|ugc)[^\\\"']*[\\\"'][^>]*>(?<text>.*?)</blockquote>"
            };

            foreach (string pattern in bodyPatterns)
            {
                Match body =
                    Regex.Match(
                        html,
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                if (!body.Success)
                {
                    continue;
                }

                string text =
                    StripHtml(
                        body.Groups["text"].Value);

                if (text.Length >= 20)
                {
                    return text.Length > 6000
                        ? text.Substring(0, 5997) + "..."
                        : text;
                }
            }

            string description =
                ExtractMetaContent(
                    html,
                    "description");

            description =
                StripHtml(description);

            return string.IsNullOrWhiteSpace(description)
                ? "Brak opisu."
                : description;
        }

        private string ExtractResourceVersion(
            string html)
        {
            string plain =
                StripHtml(html);

            Match match =
                Regex.Match(
                    plain,
                    @"\\bVersion[:\\s]+(?<value>[0-9][0-9A-Za-z._+\\-]*)",
                    RegexOptions.IgnoreCase);

            return match.Success
                ? match.Groups["value"].Value.Trim()
                : "—";
        }

        private List<string> ExtractGalleryImageUrls(
            string html,
            Uri baseUri)
        {
            List<string> urls =
                new List<string>();

            Regex imageRegex =
                new Regex(
                    "(?:href|src|data-src|data-url)=[\\\"'](?<url>[^\\\"']+)[\\\"']",
                    RegexOptions.IgnoreCase);

            foreach (Match match in imageRegex.Matches(html))
            {
                string value =
                    WebUtility.HtmlDecode(
                        match.Groups["url"].Value);

                string lower =
                    value.ToLowerInvariant();

                bool likelyContentImage =
                    lower.Contains("/attachments/") ||
                    lower.Contains("/data/attachments/") ||
                    lower.Contains("proxy.php?image=");

                if (!likelyContentImage)
                {
                    continue;
                }

                if (lower.Contains("/avatars/") ||
                    lower.Contains("/styles/") ||
                    lower.Contains("smilie") ||
                    lower.Contains("logo"))
                {
                    continue;
                }

                try
                {
                    string absolute =
                        MakeAbsoluteUrl(
                            value,
                            baseUri);

                    if (!urls.Contains(
                        absolute,
                        StringComparer.OrdinalIgnoreCase))
                    {
                        urls.Add(absolute);
                    }
                }
                catch
                {
                }
            }

            return urls;
        }

        private async Task<List<CatalogMod>> LoadModLandCatalogAsync()
        {
            const string pageUrl =
                "https://www.modland.net/beamng.drive-mods";

            string html =
                await httpClient.GetStringAsync(
                    pageUrl);

            Regex regex =
                new Regex(
                    "href=[\\\"'](?<url>/beamng\\.drive-mods/(?<category>[^/\\\"']+)/(?<slug>[^\\\"']+\\.html))[\\\"'][^>]*>(?<title>.*?)</a>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            List<CatalogMod> result =
                new List<CatalogMod>();

            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (Match match in regex.Matches(html))
            {
                string title =
                    StripHtml(
                        match.Groups["title"].Value);

                if (string.IsNullOrWhiteSpace(title) ||
                    title.Length < 3 ||
                    title.Equals(
                        "View Mod",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativeUrl =
                    WebUtility.HtmlDecode(
                        match.Groups["url"].Value);

                string absoluteUrl =
                    "https://www.modland.net" +
                    relativeUrl;

                if (!seen.Add(absoluteUrl))
                {
                    continue;
                }

                string category =
                    match.Groups["category"]
                        .Value
                        .Replace("-", " ");

                result.Add(
                    new CatalogMod
                    {
                        Title = title,
                        Source = "ModLand",
                        Author = "—",
                        Category = category,
                        Description =
                            ExtractNearbyDescription(
                                html,
                                match.Index +
                                match.Length),
                        ResourceUrl = absoluteUrl
                    });

                if (result.Count >= 30)
                {
                    break;
                }
            }

            return result;
        }

        private async Task<List<CatalogMod>> LoadModDbCatalogAsync()
        {
            const string pageUrl =
                "https://www.moddb.com/games/beamngdrive/downloads";

            string html =
                await httpClient.GetStringAsync(
                    pageUrl);

            Regex regex =
                new Regex(
                    "href=[\\\"'](?<url>/downloads/(?<slug>[^\\\"'#?]+))[\\\"'][^>]*>(?<title>.*?)</a>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            List<CatalogMod> result =
                new List<CatalogMod>();

            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (Match match in regex.Matches(html))
            {
                string relativeUrl =
                    WebUtility.HtmlDecode(
                        match.Groups["url"].Value);

                if (relativeUrl.Contains(
                    "/add",
                    StringComparison.OrdinalIgnoreCase) ||
                    relativeUrl.Contains(
                    "/start/",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string title =
                    StripHtml(
                        match.Groups["title"].Value);

                if (string.IsNullOrWhiteSpace(title) ||
                    title.Length < 3)
                {
                    continue;
                }

                string absoluteUrl =
                    "https://www.moddb.com" +
                    relativeUrl;

                if (!seen.Add(absoluteUrl))
                {
                    continue;
                }

                result.Add(
                    new CatalogMod
                    {
                        Title = title,
                        Source = "ModDB",
                        Author = "—",
                        Category = "Download",
                        Description =
                            ExtractNearbyDescription(
                                html,
                                match.Index +
                                match.Length),
                        ResourceUrl = absoluteUrl
                    });

                if (result.Count >= 20)
                {
                    break;
                }
            }

            return result;
        }

        private string ExtractThumbnailUrl(
            string html,
            Uri baseUri)
        {
            string[] patterns =
            {
                "(?:href|src|data-src|data-url)=[\\\"'](?<url>[^\\\"']*/data/resource_icons/[^\\\"']+)[\\\"']",
                "(?:href|src|data-src|data-url)=[\\\"'](?<url>[^\\\"']*resource_icons/[^\\\"']+)[\\\"']",
                "(?:href|src|data-src|data-url)=[\\\"'](?<url>[^\\\"']*/attachments/[^\\\"']+)[\\\"']",
                "(?:href|src|data-src|data-url)=[\\\"'](?<url>[^\\\"']+\\.(?:jpg|jpeg|png|webp)(?:\\?[^\\\"']*)?)[\\\"']"
            };

            foreach (string pattern in patterns)
            {
                foreach (Match match in Regex.Matches(
                    html,
                    pattern,
                    RegexOptions.IgnoreCase))
                {
                    string value =
                        WebUtility.HtmlDecode(
                            match.Groups["url"].Value);

                    string lower =
                        value.ToLowerInvariant();

                    if (value.StartsWith(
                        "data:",
                        StringComparison.OrdinalIgnoreCase) ||
                        lower.Contains("/avatars/") ||
                        lower.Contains("/styles/") ||
                        lower.Contains("logo") ||
                        lower.Contains("favicon") ||
                        lower.Contains("smilie"))
                    {
                        continue;
                    }

                    try
                    {
                        return MakeAbsoluteUrl(
                            value,
                            baseUri);
                    }
                    catch
                    {
                    }
                }
            }

            return "";
        }

        private string GetNearbyHtml(
            string html,
            int centerIndex,
            int radius)
        {
            int start =
                Math.Max(
                    0,
                    centerIndex - radius / 2);

            int length =
                Math.Min(
                    radius,
                    html.Length - start);

            return html.Substring(
                start,
                length);
        }

        private string ExtractFirstText(
            string html,
            string pattern)
        {
            Match match =
                Regex.Match(
                    html,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (!match.Success)
            {
                return "";
            }

            return StripHtml(
                match.Groups["text"].Value);
        }

        private string StripHtml(string value)
        {
            string text =
                Regex.Replace(
                    value,
                    "<[^>]+>",
                    " ");

            text =
                WebUtility.HtmlDecode(text);

            return Regex.Replace(
                    text,
                    "\\s+",
                    " ")
                .Trim();
        }

        private string ExtractNearbyDescription(
            string html,
            int startIndex)
        {
            if (startIndex < 0 ||
                startIndex >= html.Length)
            {
                return "";
            }

            int length =
                Math.Min(
                    900,
                    html.Length - startIndex);

            string fragment =
                html.Substring(
                    startIndex,
                    length);

            Match paragraph =
                Regex.Match(
                    fragment,
                    "<p[^>]*>(?<text>.*?)</p>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (!paragraph.Success)
            {
                return "";
            }

            string description =
                StripHtml(
                    paragraph.Groups["text"].Value);

            if (description.Length > 180)
            {
                description =
                    description.Substring(
                        0,
                        177) +
                    "...";
            }

            return description;
        }

        private async void DownloadCatalogItem_Click(
            object sender,
            RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is not System.Windows.Controls.Button button ||
                button.DataContext is not CatalogMod mod)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(modsFolder) ||
                !Directory.Exists(modsFolder))
            {
                CatalogStatusText.Text =
                    "Nie wykryto folderu modów BeamNG.";

                return;
            }

            button.IsEnabled = false;

            try
            {
                CatalogStatusText.Text =
                    "Przygotowanie pobierania: " +
                    mod.Title +
                    "...";

                await DownloadAndInstallCatalogModAsync(
                    mod);

                CatalogStatusText.Text =
                    "Zainstalowano: " +
                    mod.Title;

                ScanMods();
            }
            catch (Exception ex)
            {
                CatalogStatusText.Text =
                    "Błąd pobierania „" +
                    mod.Title +
                    "”: " +
                    ex.Message;
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async Task DownloadAndInstallCatalogModAsync(
            CatalogMod mod)
        {
            Uri pageUri =
                new Uri(mod.ResourceUrl);

            using HttpResponseMessage response =
                await GetDownloadResponseAsync(
                    pageUri,
                    mod.Source);

            response.EnsureSuccessStatusCode();

            string fileName =
                GetDownloadedFileName(
                    response,
                    mod.Title);

            string tempDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "BeamNGModManager");

            Directory.CreateDirectory(
                tempDirectory);

            string tempFile =
                Path.Combine(
                    tempDirectory,
                    Guid.NewGuid().ToString("N") +
                    "_" +
                    SanitizeFileName(fileName));

            try
            {
                long? totalLength =
                    response.Content.Headers.ContentLength;

                using Stream input =
                    await response.Content.ReadAsStreamAsync();

                using FileStream output =
                    new FileStream(
                        tempFile,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        useAsync: true);

                byte[] buffer =
                    new byte[81920];

                long downloaded = 0;

                while (true)
                {
                    int read =
                        await input.ReadAsync(
                            buffer,
                            0,
                            buffer.Length);

                    if (read <= 0)
                    {
                        break;
                    }

                    await output.WriteAsync(
                        buffer,
                        0,
                        read);

                    downloaded += read;

                    if (totalLength.HasValue &&
                        totalLength.Value > 0)
                    {
                        double percent =
                            downloaded *
                            100.0 /
                            totalLength.Value;

                        CatalogStatusText.Text =
                            "Pobieranie „" +
                            mod.Title +
                            "”: " +
                            percent.ToString("0") +
                            "%";
                    }
                    else
                    {
                        CatalogStatusText.Text =
                            "Pobieranie „" +
                            mod.Title +
                            "”: " +
                            FormatSize(downloaded);
                    }
                }

                string zipStatus =
                    CheckZip(tempFile);

                if (zipStatus != "OK")
                {
                    throw new InvalidDataException(
                        "Pobrany plik nie jest poprawnym archiwum ZIP.");
                }

                string modType =
                    DetectModType(tempFile);

                CompatibilityResult compatibility =
                    CheckCompatibility(
                        tempFile,
                        zipStatus,
                        modType);

                if (compatibility.Status ==
                    "Niezgodny")
                {
                    throw new InvalidDataException(
                        compatibility.Reason);
                }

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

                    if (answer !=
                        MessageBoxResult.Yes)
                    {
                        throw new OperationCanceledException(
                            "Instalacja anulowana przez użytkownika.");
                    }
                }

                string destination =
                    Path.Combine(
                        modsFolder!,
                        SanitizeFileName(fileName));

                if (File.Exists(destination))
                {
                    MessageBoxResult answer =
                        MessageBox.Show(
                            "Mod o tej nazwie już istnieje.\n\n" +
                            "Czy zastąpić istniejący plik?",
                            "Mod już istnieje",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (answer !=
                        MessageBoxResult.Yes)
                    {
                        throw new OperationCanceledException(
                            "Instalacja anulowana przez użytkownika.");
                    }
                }

                File.Copy(
                    tempFile,
                    destination,
                    true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                }
            }
        }

        private async Task<HttpResponseMessage> GetDownloadResponseAsync(
            Uri initialUri,
            string source)
        {
            Uri currentUri =
                initialUri;

            for (int attempt = 0;
                attempt < 4;
                attempt++)
            {
                HttpResponseMessage response =
                    await downloadClient.GetAsync(
                        currentUri,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                string? mediaType =
                    response.Content.Headers.ContentType?.MediaType;

                string? disposition =
                    response.Content.Headers.ContentDisposition?.DispositionType;

                bool looksLikeFile =
                    !string.IsNullOrWhiteSpace(disposition) &&
                    disposition.Equals(
                        "attachment",
                        StringComparison.OrdinalIgnoreCase);

                looksLikeFile =
                    looksLikeFile ||
                    mediaType == "application/zip" ||
                    mediaType == "application/x-zip-compressed" ||
                    mediaType == "application/octet-stream";

                if (looksLikeFile)
                {
                    return response;
                }

                if (mediaType == null ||
                    !mediaType.Contains(
                        "html",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return response;
                }

                string html =
                    await response.Content.ReadAsStringAsync();

                Uri? nextUri =
                    ExtractDownloadUriFromPage(
                        html,
                        response.RequestMessage?.RequestUri ??
                        currentUri,
                        source);

                response.Dispose();

                if (nextUri == null ||
                    nextUri == currentUri)
                {
                    throw new InvalidOperationException(
                        "Nie znaleziono bezpośredniego pliku do pobrania.");
                }

                currentUri =
                    nextUri;
            }

            throw new InvalidOperationException(
                "Strona używa zbyt wielu etapów pobierania.");
        }

        private Uri? ExtractDownloadUriFromPage(
            string html,
            Uri baseUri,
            string source)
        {
            List<string> patterns =
                new List<string>();

            if (source == "Repo BeamNG")
            {
                patterns.Add(
                    "href=[\\\"'](?<url>[^\\\"']*download\\?version=[^\\\"']+)[\\\"']");
            }
            else if (source == "ModDB")
            {
                patterns.Add(
                    "href=[\\\"'](?<url>/downloads/start/[^\\\"']+)[\\\"']");

                patterns.Add(
                    "href=[\\\"'](?<url>/downloads/[^\\\"']+)[\\\"']");
            }
            else if (source == "ModLand")
            {
                patterns.Add(
                    "href=[\\\"'](?<url>[^\\\"']+\\.zip(?:\\?[^\\\"']*)?)[\\\"']");

                patterns.Add(
                    "href=[\\\"'](?<url>[^\\\"']*(?:download|downloads)[^\\\"']*)[\\\"']");
            }

            patterns.Add(
                "href=[\\\"'](?<url>[^\\\"']+\\.zip(?:\\?[^\\\"']*)?)[\\\"']");

            foreach (string pattern in patterns)
            {
                Match match =
                    Regex.Match(
                        html,
                        pattern,
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    continue;
                }

                string href =
                    WebUtility.HtmlDecode(
                        match.Groups["url"].Value);

                if (href.Contains(
                    "/add",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Uri.TryCreate(
                    href,
                    UriKind.Absolute,
                    out Uri? absoluteUri))
                {
                    return absoluteUri;
                }

                return new Uri(
                    baseUri,
                    href);
            }

            return null;
        }

        private string GetDownloadedFileName(
            HttpResponseMessage response,
            string modTitle)
        {
            string? fileName =
                response.Content.Headers
                    .ContentDisposition?
                    .FileNameStar;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName =
                    response.Content.Headers
                        .ContentDisposition?
                        .FileName;
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                fileName =
                    fileName
                        .Trim()
                        .Trim('"');

                if (!fileName.EndsWith(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".zip";
                }

                return fileName;
            }

            string safeTitle =
                SanitizeFileName(modTitle);

            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                safeTitle = "downloaded_mod";
            }

            return safeTitle + ".zip";
        }

        private string SanitizeFileName(
            string fileName)
        {
            foreach (char invalid
                in Path.GetInvalidFileNameChars())
            {
                fileName =
                    fileName.Replace(
                        invalid,
                        '_');
            }

            return fileName;
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

    public class CatalogMod
    {
        public string Title { get; set; } = "";
        public string Source { get; set; } = "";
        public string Author { get; set; } = "—";
        public string Category { get; set; } = "Repo";
        public string Description { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public string Version { get; set; } = "—";
        public string FileSize { get; set; } = "—";
        public string TotalDownloads { get; set; } = "—";
        public string Subscriptions { get; set; } = "—";
        public string FirstRelease { get; set; } = "—";
        public string LastUpdate { get; set; } = "—";
        public string Rating { get; set; } = "—";
        public string ResourceUrl { get; set; } = "";
        public List<string> GalleryImages { get; set; } =
            new List<string>();
        public bool DetailsLoaded { get; set; }
    }

}
