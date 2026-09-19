using System;
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
    public partial class DownloadWindow : Window
    {
        private readonly string modsFolder;

        private static readonly HttpClient httpClient =
            CreateHttpClient();

        public DownloadWindow(string modsFolder)
        {
            InitializeComponent();
            this.modsFolder = modsFolder;
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClientHandler handler =
                new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate |
                        DecompressionMethods.Brotli
                };

            HttpClient client =
                new HttpClient(handler)
                {
                    Timeout =
                        TimeSpan.FromMinutes(15)
                };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BeamNGModManager/0.2");

            return client;
        }

        private void OpenBeamNgRepo_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenWebsite(
                "https://www.beamng.com/resources/");
        }

        private void OpenNexus_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenWebsite(
                "https://www.nexusmods.com/games/beamngdrive/mods");
        }

        private void OpenModDb_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenWebsite(
                "https://www.moddb.com/search?q=BeamNG");
        }

        private void OpenModLand_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenWebsite(
                "https://www.modland.net/beamng.drive-mods");
        }

        private void OpenWebsite(string url)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Nie udało się otworzyć strony: " +
                    ex.Message;
            }
        }

        private async void DownloadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string input =
                UrlTextBox.Text.Trim();

            if (!Uri.TryCreate(
                input,
                UriKind.Absolute,
                out Uri? inputUri) ||
                (inputUri.Scheme != Uri.UriSchemeHttp &&
                 inputUri.Scheme != Uri.UriSchemeHttps))
            {
                StatusText.Text =
                    "Wpisz poprawny adres HTTP lub HTTPS.";

                return;
            }

            DownloadButton.IsEnabled = false;
            DownloadProgress.Value = 0;

            string? tempFile = null;

            try
            {
                Uri downloadUri =
                    await ResolveDownloadUriAsync(
                        inputUri);

                StatusText.Text =
                    "Łączenie z serwerem...";

                using HttpResponseMessage response =
                    await httpClient.GetAsync(
                        downloadUri,
                        HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                string fileName =
                    GetFileName(
                        response,
                        downloadUri);

                if (!fileName.EndsWith(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".zip";
                }

                string tempDirectory =
                    Path.Combine(
                        Path.GetTempPath(),
                        "BeamNGModManager");

                Directory.CreateDirectory(
                    tempDirectory);

                tempFile =
                    Path.Combine(
                        tempDirectory,
                        Guid.NewGuid().ToString("N") +
                        "_" +
                        SanitizeFileName(fileName));

                long? totalLength =
                    response.Content.Headers.ContentLength;

                using Stream inputStream =
                    await response.Content.ReadAsStreamAsync();

                using FileStream outputStream =
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
                        await inputStream.ReadAsync(
                            buffer,
                            0,
                            buffer.Length);

                    if (read <= 0)
                    {
                        break;
                    }

                    await outputStream.WriteAsync(
                        buffer,
                        0,
                        read);

                    downloaded += read;

                    if (totalLength.HasValue &&
                        totalLength.Value > 0)
                    {
                        DownloadProgress.Value =
                            downloaded *
                            100.0 /
                            totalLength.Value;

                        StatusText.Text =
                            "Pobieranie: " +
                            DownloadProgress.Value
                                .ToString("0") +
                            "%";
                    }
                    else
                    {
                        StatusText.Text =
                            "Pobrano " +
                            FormatSize(downloaded) +
                            "...";
                    }
                }

                DownloadProgress.Value = 100;

                StatusText.Text =
                    "Sprawdzanie archiwum ZIP...";

                string zipStatus =
                    ValidateZip(tempFile);

                if (zipStatus != "OK")
                {
                    throw new InvalidDataException(
                        "Pobrany plik nie jest poprawnym modem ZIP. " +
                        "Stan: " +
                        zipStatus);
                }

                string destinationFile =
                    Path.Combine(
                        modsFolder,
                        SanitizeFileName(fileName));

                if (File.Exists(destinationFile))
                {
                    MessageBoxResult overwrite =
                        MessageBox.Show(
                            "Mod o tej nazwie już istnieje.\n\n" +
                            "Czy zastąpić istniejący plik?",
                            "Mod już istnieje",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (overwrite !=
                        MessageBoxResult.Yes)
                    {
                        StatusText.Text =
                            "Instalacja anulowana.";

                        return;
                    }
                }

                File.Copy(
                    tempFile,
                    destinationFile,
                    true);

                StatusText.Text =
                    "Mod pobrany, sprawdzony i zainstalowany.";

                MessageBox.Show(
                    "Mod został pobrany i zainstalowany.\n\n" +
                    Path.GetFileName(destinationFile),
                    "Gotowe",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                DownloadProgress.Value = 0;

                StatusText.Text =
                    "Błąd: " +
                    ex.Message;
            }
            finally
            {
                DownloadButton.IsEnabled = true;

                if (!string.IsNullOrWhiteSpace(tempFile))
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
        }

        private async Task<Uri> ResolveDownloadUriAsync(
            Uri inputUri)
        {
            string host =
                inputUri.Host.ToLowerInvariant();

            if (host.EndsWith("beamng.com") &&
                inputUri.AbsolutePath.Contains(
                    "/resources/",
                    StringComparison.OrdinalIgnoreCase) &&
                !inputUri.AbsolutePath.Contains(
                    "/download",
                    StringComparison.OrdinalIgnoreCase))
            {
                StatusText.Text =
                    "Odczytywanie strony Repo BeamNG...";

                string html =
                    await httpClient.GetStringAsync(
                        inputUri);

                Match match =
                    Regex.Match(
                        html,
                        "href=[\"']([^\"']*download\\?version=[^\"']+)[\"']",
                        RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    throw new InvalidOperationException(
                        "Nie znaleziono przycisku Download Now na stronie moda.");
                }

                string href =
                    WebUtility.HtmlDecode(
                        match.Groups[1].Value);

                if (Uri.TryCreate(
                    href,
                    UriKind.Absolute,
                    out Uri? absoluteUri))
                {
                    return absoluteUri;
                }

                if (href.StartsWith(
                    "/",
                    StringComparison.Ordinal))
                {
                    return new Uri(
                        inputUri.GetLeftPart(
                            UriPartial.Authority) +
                        href);
                }

                if (href.StartsWith(
                    "resources/",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(
                        inputUri.GetLeftPart(
                            UriPartial.Authority) +
                        "/" +
                        href);
                }

                return new Uri(
                    inputUri,
                    href);
            }

            if (host.Contains("nexusmods.com") &&
                !inputUri.AbsolutePath.EndsWith(
                    ".zip",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Strony modów Nexus wymagają integracji z oficjalnym API. " +
                    "Na razie wklej bezpośredni link do pliku ZIP.");
            }

            return inputUri;
        }

        private string GetFileName(
            HttpResponseMessage response,
            Uri downloadUri)
        {
            string? fileName =
                response
                    .Content
                    .Headers
                    .ContentDisposition?
                    .FileNameStar;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName =
                    response
                        .Content
                        .Headers
                        .ContentDisposition?
                        .FileName;
            }

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName
                    .Trim()
                    .Trim('"');
            }

            string pathName =
                Path.GetFileName(
                    downloadUri.LocalPath);

            if (!string.IsNullOrWhiteSpace(pathName) &&
                pathName.Contains('.'))
            {
                return pathName;
            }

            return "downloaded_mod.zip";
        }

        private string ValidateZip(string file)
        {
            try
            {
                using ZipArchive archive =
                    ZipFile.OpenRead(file);

                if (archive.Entries.Count == 0)
                {
                    return "Pusty ZIP";
                }

                foreach (ZipArchiveEntry entry
                    in archive.Entries)
                {
                    if (string.IsNullOrEmpty(
                        entry.Name))
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

        private string FormatSize(long bytes)
        {
            double size = bytes;

            if (size >=
                1024L * 1024 * 1024)
            {
                return
                    $"{size / (1024 * 1024 * 1024):0.00} GB";
            }

            if (size >=
                1024L * 1024)
            {
                return
                    $"{size / (1024 * 1024):0.00} MB";
            }

            if (size >= 1024)
            {
                return
                    $"{size / 1024:0.00} KB";
            }

            return
                $"{size:0} B";
        }
    }
}
