using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BeamNGModManager
{
    public static class Localization
    {
        public static string CurrentLanguage { get; set; } = "pl";

        private static readonly (string Pl, string En)[] UiPairs =
        {
            ("NAWIGACJA", "NAVIGATION"),
            ("Biblioteka modów", "Mod library"),
            ("Pobieranie modów", "Download mods"),
            ("Odśwież bibliotekę", "Refresh library"),
            ("Sprawdź aktualizacje", "Check updates"),
            ("NARZĘDZIA", "TOOLS"),
            ("Sprawdź BeamNG", "Check BeamNG"),
            ("Zainstaluj mod", "Install mod"),
            ("Język", "Language"),
            ("Polski", "Polish"),
            ("Projekt w budowie", "Work in progress"),
            ("Podgląd interfejsu v0.2", "Interface preview v0.2"),
            ("Zarządzaj modami, zgodnością i aktualizacjami", "Manage mods, compatibility and updates"),
            ("Najpierw wykryj BeamNG.", "Detect BeamNG first."),
            ("Źródło", "Source"),
            ("Typ", "Type"),
            ("Wersja", "Version"),
            ("Aktualizacje", "Updates"),
            ("Kolizje", "Conflicts"),
            ("Rozmiar", "Size"),
            ("Plik zmieniony", "File modified"),
            ("Stan", "Status"),
            ("Zgodność", "Compatibility"),
            ("Pełne Repo BeamNG — kolejne strony doczytują się podczas przewijania", "Full BeamNG Repo — more pages load as you scroll"),
            ("Szukaj moda po nazwie", "Search mod by name"),
            ("Data publikacji — najnowsze", "Publication date — newest"),
            ("Najczęściej pobierane", "Most downloaded"),
            ("Największy rozmiar", "Largest size"),
            ("Odśwież", "Refresh"),
            ("Katalog nie został jeszcze wczytany.", "Catalog has not been loaded yet."),
            ("← Wróć do katalogu", "← Back to catalog"),
            ("Kategoria", "Category"),
            ("Pobrania", "Downloads"),
            ("Subskrypcje", "Subscriptions"),
            ("Ocena", "Rating"),
            ("Ostatnia aktualizacja", "Last update"),
            ("Opis", "Description"),
            ("Galeria", "Gallery"),
            ("Szczegóły moda", "Mod details"),
            ("Miniatura moda pojawi się tutaj", "Mod thumbnail will appear here"),
            ("Wybierz mod z listy", "Select a mod from the list"),
            ("Brak wybranego moda", "No mod selected"),
            ("Stan pliku", "File status"),
            ("Brak danych o kolizjach", "No conflict data"),
            ("Brak szczegółów analizy", "No analysis details"),
            ("Podgląd obrazu moda i dodatkowe akcje dodamy w następnym kroku.", "Mod image preview and additional actions will be added in the next step.")
        };

        private static readonly (string Pl, string En)[] ValuePairs =
        {
            ("Nie podano", "Not provided"),
            ("Nie sprawdzono", "Not checked"),
            ("Brak danych repo", "No repository data"),
            ("Brak źródła online", "No online source"),
            ("Brak danych źródła", "No source data"),
            ("Brak", "None"),
            ("Zgodny", "Compatible"),
            ("Prawdopodobnie zgodny", "Probably compatible"),
            ("Ryzyko problemów", "Risk of issues"),
            ("Niezgodny", "Incompatible"),
            ("Internet / ręczny", "Internet / manual"),
            ("Pojazd + mapa", "Vehicle + map"),
            ("Pojazd", "Vehicle"),
            ("Mapa", "Map"),
            ("Scenariusz", "Scenario"),
            ("Skrypt", "Script"),
            ("Inny", "Other"),
            ("Aktualny", "Up to date"),
            ("Nie odczytano wersji", "Version unavailable"),
            ("Błąd połączenia", "Connection error"),
            ("Błąd", "Error"),
            ("Uszkodzony", "Damaged"),
            ("Nie wykryto plików nadpisywanych przez inne mody.", "No files overwritten by other mods were detected."),
            ("Nie rozpoznano typowej struktury moda BeamNG.", "A typical BeamNG mod structure was not recognized."),
            ("Wewnątrz moda znajduje się dodatkowy ZIP.", "The mod contains an additional ZIP archive."),
            ("Archiwum jest sprawne, ale typ moda nie jest pewny.", "The archive is valid, but the mod type is uncertain."),
            ("Archiwum i struktura moda wyglądają poprawnie.", "The archive and mod structure look correct."),
            ("Nie udało się przeanalizować moda.", "The mod could not be analyzed.")
        };

        private static readonly (string Pl, string En)[] MessagePairs =
        {
            ("Nie znaleziono BeamNG lub folderu użytkownika.", "BeamNG or the user folder was not found."),
            ("BeamNG wykryty poprawnie. Wersja:", "BeamNG detected successfully. Version:"),
            ("Folder modów:", "Mods folder:"),
            ("Nie wykryto BeamNG. Użyj „Sprawdź BeamNG” w sekcji Narzędzia.", "BeamNG was not detected. Use “Check BeamNG” in Tools."),
            ("Błąd skanowania:", "Scan error:"),
            ("Biblioteka odświeżona. Wersja BeamNG:", "Library refreshed. BeamNG version:"),
            ("Znaleziono modów:", "Mods found:"),
            ("Nie znaleziono modów z Repo BeamNG do sprawdzenia.", "No BeamNG Repo mods were found to check."),
            ("Sprawdzono aktualizacje Repo BeamNG:", "BeamNG Repo updates checked:"),
            ("Dostępne aktualizacje:", "Available updates:"),
            ("Wczytywanie szczegółów:", "Loading details:"),
            ("Szczegóły:", "Details:"),
            ("Zainstalowano:", "Installed:"),
            ("Błąd pobierania", "Download error"),
            ("Pobieranie pierwszej strony Repo BeamNG...", "Loading the first BeamNG Repo page..."),
            ("Załadowano cały dostępny katalog:", "Loaded the entire available catalog:"),
            ("Załadowano", "Loaded"),
            ("modów. Przewiń niżej, aby doczytać kolejne.", "mods. Scroll down to load more."),
            ("modów | strona", "mods | page"),
            ("Przewiń niżej po kolejne.", "Scroll down for more."),
            ("Nie udało się wczytać Repo BeamNG:", "Failed to load BeamNG Repo:"),
            ("Nie udało się doczytać strony", "Failed to load page"),
            ("Doczytywanie strony", "Loading page"),
            ("Pobieranie „", "Downloading “"),
            ("Przygotowanie pobierania:", "Preparing download:"),
            ("Nie wykryto folderu modów BeamNG.", "BeamNG mods folder was not detected."),
            ("Repo zwróciło stronę pośrednią. Próba pobrania właściwego ZIP...", "Repo returned an intermediate page. Trying to download the actual ZIP..."),
            ("Nie udało się pobrać poprawnego archiwum ZIP.", "Failed to download a valid ZIP archive."),
            ("Nie znaleziono prawidłowego linku pobierania moda.", "A valid mod download link was not found."),
            ("Strona używa zbyt wielu etapów pobierania.", "The page uses too many download steps."),
            ("Gotowe:", "Ready:"),
            ("Znaleziono:", "Found:"),
            ("modów dla „", "mods for “"),
            ("modów z Repo BeamNG", "mods from BeamNG Repo"),
            ("najczęściej pobierane", "most downloaded"),
            ("największy rozmiar wśród załadowanych", "largest size among loaded"),
            ("data publikacji", "publication date")
        };

        public static string T(string pl, string en) =>
            CurrentLanguage == "en" ? en : pl;

        public static string TranslateUiLiteral(string value)
        {
            return TranslateExact(value, UiPairs);
        }

        public static string TranslateValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string exact = TranslateExact(value, ValuePairs);

            if (exact != value)
            {
                return exact;
            }

            if (CurrentLanguage == "en")
            {
                if (value.StartsWith("Aktualizacja: ", StringComparison.OrdinalIgnoreCase))
                    return "Update: " + value.Substring("Aktualizacja: ".Length);

                if (value.StartsWith("Błąd HTTP ", StringComparison.OrdinalIgnoreCase))
                    return "HTTP error " + value.Substring("Błąd HTTP ".Length);

                if (value.StartsWith("Wspólne pliki z: ", StringComparison.OrdinalIgnoreCase))
                    return "Shared files with: " + value.Substring("Wspólne pliki z: ".Length);

                return value
                    .Replace(" plików / ", " files / ", StringComparison.OrdinalIgnoreCase)
                    .Replace(" modów", " mods", StringComparison.OrdinalIgnoreCase);
            }

            if (value.StartsWith("Update: ", StringComparison.OrdinalIgnoreCase))
                return "Aktualizacja: " + value.Substring("Update: ".Length);

            if (value.StartsWith("HTTP error ", StringComparison.OrdinalIgnoreCase))
                return "Błąd HTTP " + value.Substring("HTTP error ".Length);

            if (value.StartsWith("Shared files with: ", StringComparison.OrdinalIgnoreCase))
                return "Wspólne pliki z: " + value.Substring("Shared files with: ".Length);

            return value
                .Replace(" files / ", " plików / ", StringComparison.OrdinalIgnoreCase)
                .Replace(" mods", " modów", StringComparison.OrdinalIgnoreCase);
        }

        public static string TranslateMessage(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            string result = value;
            IEnumerable<(string Pl, string En)> pairs =
                MessagePairs.OrderByDescending(pair => pair.Pl.Length);

            if (CurrentLanguage == "en")
            {
                foreach (var pair in pairs)
                {
                    result = result.Replace(
                        pair.Pl,
                        pair.En,
                        StringComparison.OrdinalIgnoreCase);
                }
            }
            else
            {
                foreach (var pair in pairs.OrderByDescending(pair => pair.En.Length))
                {
                    result = result.Replace(
                        pair.En,
                        pair.Pl,
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            return result;
        }

        private static string TranslateExact(
            string value,
            IEnumerable<(string Pl, string En)> pairs)
        {
            foreach (var pair in pairs)
            {
                if (value.Equals(pair.Pl, StringComparison.Ordinal))
                    return CurrentLanguage == "en" ? pair.En : pair.Pl;

                if (value.Equals(pair.En, StringComparison.Ordinal))
                    return CurrentLanguage == "en" ? pair.En : pair.Pl;
            }

            return value;
        }
    }

    public partial class MainWindow
    {
        private bool isApplyingLanguage;
        private bool isTranslatingStatus;

        private void InitializeLanguage()
        {
            string language =
                LoadSavedLanguage();

            Localization.CurrentLanguage =
                language;

            isApplyingLanguage = true;

            LanguageComboBox.SelectedIndex =
                language == "en"
                    ? 1
                    : 0;

            isApplyingLanguage = false;

            DependencyPropertyDescriptor
                .FromProperty(
                    TextBlock.TextProperty,
                    typeof(TextBlock))
                .AddValueChanged(
                    StatusText,
                    (_, _) => TranslateStatusBlock(StatusText));

            DependencyPropertyDescriptor
                .FromProperty(
                    TextBlock.TextProperty,
                    typeof(TextBlock))
                .AddValueChanged(
                    CatalogStatusText,
                    (_, _) => TranslateStatusBlock(CatalogStatusText));

            ApplyLanguageToUi();
        }

        private void LanguageComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            // SelectedIndex z XAML może wywołać event jeszcze w trakcie
            // InitializeComponent, zanim powstaną pozostałe kontrolki.
            if (!IsInitialized ||
                StatusText == null ||
                ModsGrid == null ||
                isApplyingLanguage ||
                LanguageComboBox?.SelectedItem is not ComboBoxItem selectedItem)
            {
                return;
            }

            string language =
                selectedItem.Tag?.ToString() == "en"
                    ? "en"
                    : "pl";

            Localization.CurrentLanguage =
                language;

            SaveLanguage(language);
            ApplyLanguageToUi();
        }

        private void ApplyLanguageToUi()
        {
            TranslateElementTree(this);

            foreach (DataGridColumn column in ModsGrid.Columns)
            {
                if (column.Header is string header)
                {
                    column.Header =
                        Localization.TranslateUiLiteral(header);
                }
            }

            TranslateStatusBlock(StatusText);
            TranslateStatusBlock(CatalogStatusText);

            RefreshGrid();
            UpdateCatalogInstallationState();
            ApplyCatalogSearch();
        }

        private void TranslateElementTree(DependencyObject parent)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is TextBlock textBlock &&
                    !string.IsNullOrWhiteSpace(textBlock.Text))
                {
                    textBlock.Text =
                        Localization.TranslateUiLiteral(textBlock.Text);
                }
                else if (child is Button button &&
                         button.Content is string buttonText)
                {
                    button.Content =
                        Localization.TranslateUiLiteral(buttonText);
                }
                else if (child is ComboBoxItem comboItem &&
                         comboItem.Content is string comboText)
                {
                    comboItem.Content =
                        Localization.TranslateUiLiteral(comboText);
                }
                else if (child is FrameworkElement element &&
                         element.ToolTip is string toolTip)
                {
                    element.ToolTip =
                        Localization.TranslateUiLiteral(toolTip);
                }

                if (child is DependencyObject dependencyObject)
                {
                    TranslateElementTree(dependencyObject);
                }
            }
        }

        private void TranslateStatusBlock(
            TextBlock textBlock)
        {
            if (isTranslatingStatus)
            {
                return;
            }

            string translated =
                Localization.TranslateMessage(
                    textBlock.Text);

            if (translated == textBlock.Text)
            {
                return;
            }

            try
            {
                isTranslatingStatus = true;
                textBlock.Text = translated;
            }
            finally
            {
                isTranslatingStatus = false;
            }
        }

        private string LoadSavedLanguage()
        {
            try
            {
                string file =
                    GetLanguageFilePath();

                if (File.Exists(file))
                {
                    string value =
                        File.ReadAllText(file)
                            .Trim()
                            .ToLowerInvariant();

                    if (value == "en")
                    {
                        return "en";
                    }
                }
            }
            catch
            {
            }

            return "pl";
        }

        private void SaveLanguage(
            string language)
        {
            try
            {
                string file =
                    GetLanguageFilePath();

                Directory.CreateDirectory(
                    Path.GetDirectoryName(file)!);

                File.WriteAllText(
                    file,
                    language);
            }
            catch
            {
            }
        }

        private string GetLanguageFilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "BeamNGModManager",
                "language.txt");
        }
    }
}
