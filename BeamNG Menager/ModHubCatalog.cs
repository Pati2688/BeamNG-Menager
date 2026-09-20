using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BeamNGModManager
{
    public partial class MainWindow
    {
        private string currentCatalogSource = "beamng";

        private async void CatalogSourceComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (CatalogSourceComboBox?.SelectedItem is not ComboBoxItem selectedItem)
            {
                return;
            }

            currentCatalogSource =
                selectedItem.Tag?.ToString() ?? "beamng";

            if (!IsInitialized ||
                CatalogTitleText == null ||
                CatalogSubtitleText == null ||
                CatalogSearchBox == null ||
                CatalogBrowseView == null ||
                CatalogDetailView == null)
            {
                return;
            }

            if (currentCatalogSource == "modhub")
            {
                CatalogTitleText.Text = "ModHub";
                CatalogSubtitleText.Text =
                    Localization.T(
                        "Mody BeamNG.drive z ModHub — kolejne strony doczytują się podczas przewijania",
                        "BeamNG.drive mods from ModHub — more pages load while scrolling");
            }
            else
            {
                CatalogTitleText.Text = "Repo BeamNG";
                CatalogSubtitleText.Text =
                    Localization.T(
                        "Pełne Repo BeamNG — kolejne strony doczytują się podczas przewijania",
                        "Full BeamNG Repo — more pages load while scrolling");
            }

            CatalogDetailView.Visibility =
                Visibility.Collapsed;

            CatalogBrowseView.Visibility =
                Visibility.Visible;

            CatalogSearchBox.Text = "";

            if (CatalogScrollViewer != null)
            {
                CatalogScrollViewer.ScrollToTop();
            }

            await LoadCatalogAsync();
        }

        private Task<List<CatalogMod>> LoadSelectedCatalogPageAsync(
            int page)
        {
            return currentCatalogSource == "modhub"
                ? LoadModHubCatalogPageAsync(page)
                : LoadBeamNgCatalogPageAsync(page);
        }

        private string CurrentCatalogSourceName =>
            currentCatalogSource == "modhub"
                ? "ModHub"
                : "Repo BeamNG";

        private async Task<List<CatalogMod>> LoadModHubCatalogPageAsync(
            int page)
        {
            string pageUrl =
                page <= 1
                    ? "https://www.modhub.us/category/beamng-drive-mods"
                    : "https://www.modhub.us/category/beamng-drive-mods/page/" + page;

            string html =
                await httpClient.GetStringAsync(
                    pageUrl);

            Uri pageUri =
                new Uri(pageUrl);

            Regex linkRegex =
                new Regex(
                    "href=[\\\"'](?<url>(?:https://www\\.modhub\\.us)?/beamng-drive-mods/(?<slug>[^\\\"'#?/<]+))/?[\\\"'][^>]*>(?<title>.*?)</a>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            List<CatalogMod> result =
                new List<CatalogMod>();

            HashSet<string> seen =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (Match match in linkRegex.Matches(html))
            {
                string absoluteUrl =
                    MakeAbsoluteUrl(
                        WebUtility.HtmlDecode(
                            match.Groups["url"].Value),
                        pageUri);

                if (!seen.Add(absoluteUrl))
                {
                    continue;
                }

                string title =
                    StripHtml(
                        match.Groups["title"].Value);

                if (!IsValidModHubTitle(title))
                {
                    title =
                        HumanizeModHubSlug(
                            match.Groups["slug"].Value);
                }

                if (!IsValidModHubTitle(title))
                {
                    continue;
                }

                string nearby =
                    GetNearbyHtml(
                        html,
                        match.Index,
                        3200);

                string description =
                    ExtractNearbyDescription(
                        nearby,
                        0);

                string thumbnail =
                    ExtractThumbnailUrl(
                        nearby,
                        pageUri);

                result.Add(
                    new CatalogMod
                    {
                        Title = title,
                        Source = "ModHub",
                        Author = "—",
                        Category = "BeamNG Drive Mods",
                        Description = description,
                        ThumbnailUrl = thumbnail,
                        ResourceUrl = absoluteUrl,
                        InstallButtonText =
                            Localization.T(
                                "Pobierz",
                                "Download"),
                        InstallDetailButtonText =
                            Localization.T(
                                "Pobierz i zainstaluj",
                                "Download and install")
                    });
            }

            return result;
        }

        private bool IsValidModHubTitle(
            string title)
        {
            if (string.IsNullOrWhiteSpace(title) ||
                title.Length < 3)
            {
                return false;
            }

            string[] invalid =
            {
                "Download",
                "Read More",
                "BeamNG Drive Mods",
                "Beamng drive mods",
                "See All"
            };

            return !invalid.Any(value =>
                title.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase));
        }

        private string HumanizeModHubSlug(
            string slug)
        {
            string text =
                WebUtility.UrlDecode(slug)
                    .Replace("-", " ")
                    .Replace("_", " ");

            text =
                Regex.Replace(
                    text,
                    @"\s+",
                    " ")
                .Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return char.ToUpperInvariant(text[0]) +
                text.Substring(1);
        }

        private async Task EnhanceModHubCatalogAsync(
            List<CatalogMod> mods)
        {
            IEnumerable<Task> tasks =
                mods.Select(
                    async mod =>
                    {
                        await catalogThumbnailSemaphore.WaitAsync();

                        try
                        {
                            await LoadModHubModDetailsAsync(
                                mod);
                        }
                        catch
                        {
                            // Katalog nadal ma działać, nawet jeśli
                            // pojedyncza karta ModHub chwilowo nie odpowiada.
                        }
                        finally
                        {
                            catalogThumbnailSemaphore.Release();
                        }
                    });

            await Task.WhenAll(
                tasks);
        }

        private async Task LoadModHubModDetailsAsync(
            CatalogMod mod)
        {
            string html =
                await httpClient.GetStringAsync(
                    mod.ResourceUrl);

            string plain =
                StripHtml(html);

            string description =
                ExtractBetweenLabels(
                    plain,
                    "DESCRIPTION",
                    "CREDITS");

            if (description == "—")
            {
                description =
                    ExtractMetaContent(
                        html,
                        "description");
            }

            if (!string.IsNullOrWhiteSpace(description) &&
                description != "—")
            {
                mod.Description =
                    description.Length > 6000
                        ? description.Substring(0, 5997) + "..."
                        : description;
            }

            string downloads =
                ExtractRegexValue(
                    plain,
                    @"(?<value>[0-9][0-9,.\s]*)\s+Downloads\b");

            if (downloads != "—")
            {
                mod.TotalDownloads =
                    downloads;

                mod.DownloadCountValue =
                    ParseCountValue(
                        downloads);
            }

            string likes =
                ExtractRegexValue(
                    plain,
                    @"(?<value>[0-9][0-9,.\s]*)\s+Likes\b");

            if (likes != "—")
            {
                mod.Rating =
                    likes +
                    Localization.T(
                        " polubień",
                        " likes");
            }

            Uri resourceUri =
                new Uri(mod.ResourceUrl);

            List<string> imageUrls =
                ExtractModHubImageUrls(
                    html,
                    resourceUri);

            string ogImage =
                ExtractMetaContent(
                    html,
                    "og:image");

            if (!string.IsNullOrWhiteSpace(ogImage))
            {
                try
                {
                    string absoluteOgImage =
                        MakeAbsoluteUrl(
                            ogImage,
                            resourceUri);

                    imageUrls.RemoveAll(url =>
                        url.Equals(
                            absoluteOgImage,
                            StringComparison.OrdinalIgnoreCase));

                    imageUrls.Insert(
                        0,
                        absoluteOgImage);
                }
                catch
                {
                }
            }

            if (imageUrls.Count == 0)
            {
                string fallback =
                    ExtractThumbnailUrl(
                        html,
                        resourceUri);

                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    imageUrls.Add(
                        fallback);
                }
            }

            List<string> cachedImages =
                await CacheGalleryImagesAsync(
                    imageUrls,
                    mod.ResourceUrl);

            if (cachedImages.Count > 0)
            {
                mod.ThumbnailUrl =
                    cachedImages[0];

                mod.GalleryImages =
                    cachedImages;
            }

            string version =
                ExtractVersionFromModHubTitle(
                    mod.Title);

            if (!string.IsNullOrWhiteSpace(version))
            {
                mod.Version =
                    version;
            }

            mod.DetailsLoaded = true;
        }

        private string ExtractVersionFromModHubTitle(
            string title)
        {
            Match match =
                Regex.Match(
                    title,
                    @"\bv(?:ersion\s*)?(?<value>[0-9][0-9A-Za-z._+\-]*)\b",
                    RegexOptions.IgnoreCase);

            return match.Success
                ? match.Groups["value"].Value
                : "—";
        }

        private List<string> ExtractModHubImageUrls(
            string html,
            Uri baseUri)
        {
            List<string> urls =
                new List<string>();

            Regex imageRegex =
                new Regex(
                    "<img\\b[^>]*(?:src|data-src|data-original|data-lazy-src)=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            foreach (Match match in imageRegex.Matches(html))
            {
                string value =
                    WebUtility.HtmlDecode(
                        match.Groups["url"].Value)
                    .Trim();

                string lower =
                    value.ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(value) ||
                    value.StartsWith(
                        "data:",
                        StringComparison.OrdinalIgnoreCase) ||
                    lower.Contains("logo") ||
                    lower.Contains("favicon") ||
                    lower.Contains("avatar") ||
                    lower.Contains("icon") ||
                    lower.Contains("flag") ||
                    lower.Contains("banner") ||
                    lower.Contains("placeholder") ||
                    lower.Contains("yandex") ||
                    lower.Contains("google") ||
                    lower.Contains("doubleclick") ||
                    lower.Contains("/ads/"))
                {
                    continue;
                }

                bool looksLikeImage =
                    Regex.IsMatch(
                        lower,
                        @"\\.(?:jpg|jpeg|png|webp)(?:\\?|$)",
                        RegexOptions.IgnoreCase);

                if (!looksLikeImage)
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
                        urls.Add(
                            absolute);
                    }
                }
                catch
                {
                }
            }

            return urls
                .Take(12)
                .ToList();
        }
    }
}
