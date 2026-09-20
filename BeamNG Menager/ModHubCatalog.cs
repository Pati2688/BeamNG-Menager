using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
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
                    "<a\\b[^>]*href=[\\\"'](?<url>(?:https://www\\.modhub\\.us)?/beamng-drive-mods/(?<slug>[^\\\"'#?/<]+))/?[\\\"'][^>]*>(?<content>.*?)</a>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            Dictionary<string, List<Match>> matchesByUrl =
                new Dictionary<string, List<Match>>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (Match match in linkRegex.Matches(html))
            {
                string slug =
                    WebUtility.UrlDecode(
                        match.Groups["slug"].Value)
                    .Trim()
                    .Trim('/');

                if (string.IsNullOrWhiteSpace(slug))
                {
                    continue;
                }

                // Nie polegamy tutaj na ogólnym resolverze URL.
                // Dla ModHub budujemy kanoniczny adres strony moda bezpośrednio
                // z przechwyconego sluga, żeby ResourceUrl nigdy nie stał się "/".
                string absoluteUrl =
                    BuildModHubResourceUrlFromSlug(
                        slug);

                if (!matchesByUrl.TryGetValue(
                    absoluteUrl,
                    out List<Match>? group))
                {
                    group =
                        new List<Match>();

                    matchesByUrl[absoluteUrl] =
                        group;
                }

                group.Add(
                    match);
            }

            List<CatalogMod> result =
                new List<CatalogMod>();

            foreach (KeyValuePair<string, List<Match>> item
                in matchesByUrl)
            {
                List<Match> group =
                    item.Value;

                Match representative =
                    group[0];

                string title =
                    group
                        .Select(match =>
                            StripHtml(
                                match.Groups["content"].Value))
                        .FirstOrDefault(
                            IsValidModHubTitle) ??
                    "";

                if (!IsValidModHubTitle(title))
                {
                    title =
                        group
                            .Select(match =>
                                ExtractModHubImageAlt(
                                    match.Groups["content"].Value))
                            .FirstOrDefault(
                                IsValidModHubTitle) ??
                        "";
                }

                if (!IsValidModHubTitle(title))
                {
                    title =
                        HumanizeModHubSlug(
                            representative.Groups["slug"].Value);
                }

                if (!IsValidModHubTitle(title))
                {
                    continue;
                }

                string thumbnail =
                    group
                        .Select(match =>
                            ExtractModHubImageFromAnchor(
                                match.Groups["content"].Value,
                                pageUri))
                        .FirstOrDefault(value =>
                            !string.IsNullOrWhiteSpace(value)) ??
                    "";

                Match titleMatch =
                    group.FirstOrDefault(match =>
                        IsValidModHubTitle(
                            StripHtml(
                                match.Groups["content"].Value))) ??
                    representative;

                string nearby =
                    GetNearbyHtml(
                        html,
                        titleMatch.Index,
                        3200);

                string description =
                    ExtractNearbyDescription(
                        nearby,
                        0);

                result.Add(
                    new CatalogMod
                    {
                        Title = title,
                        Source = "ModHub",
                        Author = "—",
                        Category = "BeamNG Drive Mods",
                        Description = description,
                        ThumbnailUrl = thumbnail,
                        ResourceUrl = item.Key,
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

        private string BuildModHubResourceUrlFromSlug(
            string slug)
        {
            return
                "https://www.modhub.us/beamng-drive-mods/" +
                slug.Trim().Trim('/');
        }

        private string ResolveModHubResourceUrl(
            CatalogMod mod)
        {
            if (Uri.TryCreate(
                    mod.ResourceUrl,
                    UriKind.Absolute,
                    out Uri? existing) &&
                existing.Host.EndsWith(
                    "modhub.us",
                    StringComparison.OrdinalIgnoreCase) &&
                existing.AbsolutePath.StartsWith(
                    "/beamng-drive-mods/",
                    StringComparison.OrdinalIgnoreCase) &&
                existing.AbsolutePath.Length >
                    "/beamng-drive-mods/".Length)
            {
                return existing.ToString();
            }

            string slug =
                WebUtility.HtmlDecode(
                    mod.Title)
                .ToLowerInvariant();

            slug =
                Regex.Replace(
                    slug,
                    @"[^a-z0-9]+",
                    "-")
                .Trim('-');

            return
                BuildModHubResourceUrlFromSlug(
                    slug);
        }

        private string ExtractModHubImageAlt(
            string anchorContent)
        {
            Match altMatch =
                Regex.Match(
                    anchorContent,
                    "<img\\b[^>]*\\balt=[\\\"'](?<alt>[^\\\"']+)[\\\"'][^>]*>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            return altMatch.Success
                ? WebUtility.HtmlDecode(
                    altMatch.Groups["alt"].Value)
                    .Trim()
                : "";
        }

        private string ExtractModHubImageFromAnchor(
            string anchorContent,
            Uri baseUri)
        {
            Regex imageRegex =
                new Regex(
                    "<(?:img|source)\\b[^>]*(?:src|data-src|data-original|data-lazy-src|srcset|data-srcset)=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            foreach (Match imageMatch in imageRegex.Matches(
                anchorContent))
            {
                string value =
                    WebUtility.HtmlDecode(
                        imageMatch.Groups["url"].Value)
                    .Trim();

                if (value.Contains(','))
                {
                    value =
                        value.Split(',')[0]
                            .Trim();
                }

                int descriptorSeparator =
                    value.IndexOf(' ');

                if (descriptorSeparator > 0)
                {
                    value =
                        value.Substring(
                            0,
                            descriptorSeparator);
                }

                string lower =
                    value.ToLowerInvariant();

                bool validImage =
                    lower.Contains("/mod/download-photo/") ||
                    lower.Contains("/uploads/images/photos/") ||
                    Regex.IsMatch(
                        lower,
                        @"\.(?:jpg|jpeg|png|webp)(?:\?|$)",
                        RegexOptions.IgnoreCase);

                if (!validImage ||
                    lower.Contains("avatar") ||
                    lower.Contains("profile") ||
                    lower.Contains("placeholder") ||
                    lower.Contains("logo") ||
                    lower.Contains("icon"))
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

            return "";
        }

        private string ExtractClosestModHubPhoto(
            string html,
            int anchorIndex,
            Uri baseUri)
        {
            Regex photoRegex =
                new Regex(
                    "(?<url>(?:https://www\\.modhub\\.us)?/uploads/images/photos/[^\\\"'<>\\s]+?\\.(?:webp|jpg|jpeg|png)(?:\\?[^\\\"'<>\\s]*)?)",
                    RegexOptions.IgnoreCase);

            Match? bestMatch =
                null;

            int bestDistance =
                int.MaxValue;

            foreach (Match imageMatch in photoRegex.Matches(
                html))
            {
                int distance =
                    Math.Abs(
                        imageMatch.Index -
                        anchorIndex);

                if (distance > 6000)
                {
                    continue;
                }

                string candidate =
                    WebUtility.HtmlDecode(
                        imageMatch.Groups["url"].Value);

                // Miniatury kart mają zwykle prefiks thumb_.
                // Jeśli taki obraz jest blisko linku moda, traktujemy go priorytetowo.
                bool isThumb =
                    candidate.Contains(
                        "/thumb_",
                        StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains(
                        "thumb_",
                        StringComparison.OrdinalIgnoreCase);

                int adjustedDistance =
                    isThumb
                        ? Math.Max(
                            0,
                            distance - 1500)
                        : distance;

                if (adjustedDistance >=
                    bestDistance)
                {
                    continue;
                }

                bestDistance =
                    adjustedDistance;

                bestMatch =
                    imageMatch;
            }

            if (bestMatch == null)
            {
                return "";
            }

            try
            {
                return MakeAbsoluteUrl(
                    WebUtility.HtmlDecode(
                        bestMatch.Groups["url"].Value),
                    baseUri);
            }
            catch
            {
                return "";
            }
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
            // ModHub ładuje obrazy w sposób, którego nie da się niezawodnie
            // odczytać z samej listy kategorii. Pobieramy więc tylko metadane
            // obrazka z konkretnej strony moda. Robimy to maksymalnie po 2
            // naraz, bez pobierania całej galerii, żeby nie blokować interfejsu.
            using SemaphoreSlim thumbnailLimit =
                new SemaphoreSlim(2);

            Task[] tasks =
                mods
                    .Select(
                        async mod =>
                        {
                            await thumbnailLimit.WaitAsync();

                            try
                            {
                                await LoadModHubThumbnailAsync(
                                    mod);
                            }
                            catch
                            {
                                // Brak miniatury nie może blokować katalogu.
                            }
                            finally
                            {
                                thumbnailLimit.Release();
                            }
                        })
                    .ToArray();

            await Task.WhenAll(
                tasks);
        }

        private async Task LoadModHubThumbnailAsync(
            CatalogMod mod)
        {
            string originalThumbnail =
                mod.ThumbnailUrl ?? "";

            // Miniatura z listy kategorii pojawia się od razu i w wielu
            // przypadkach jest już właściwym zdjęciem moda. Nie nadpisujemy
            // jej później przypadkowym avatarem lub placeholderem ze strony
            // szczegółów.
            if (!string.IsNullOrWhiteSpace(
                    originalThumbnail) &&
                IsModHubPhotoUrl(
                    originalThumbnail))
            {
                string cachedOriginal =
                    await CacheRemoteImageAsync(
                        originalThumbnail,
                        mod.ResourceUrl,
                        "modhub-thumbs");

                if (!string.IsNullOrWhiteSpace(
                    cachedOriginal))
                {
                    mod.ThumbnailUrl =
                        cachedOriginal;
                }

                return;
            }

            string html =
                await httpClient.GetStringAsync(
                    mod.ResourceUrl);

            Uri resourceUri =
                new Uri(mod.ResourceUrl);

            List<string> candidates =
                ExtractModHubPhotoUrls(
                    html,
                    resourceUri);

            if (candidates.Count == 0)
            {
                candidates =
                    ExtractModHubImageUrls(
                        html,
                        resourceUri)
                    .Where(IsLikelyRealModImage)
                    .ToList();
            }

            foreach (string candidate in candidates)
            {
                string cached =
                    await CacheRemoteImageAsync(
                        candidate,
                        mod.ResourceUrl,
                        "modhub-thumbs");

                if (string.IsNullOrWhiteSpace(
                    cached))
                {
                    continue;
                }

                mod.ThumbnailUrl =
                    cached;

                return;
            }

            // Jeśli ulepszenie się nie uda, zostawiamy miniaturę z listy
            // zamiast ją usuwać lub zastępować błędnym obrazem.
            mod.ThumbnailUrl =
                originalThumbnail;
        }

        private List<string> ExtractModHubPhotoUrls(
            string html,
            Uri baseUri)
        {
            Regex photoRegex =
                new Regex(
                    "(?<url>(?:https://www\\.modhub\\.us)?/uploads/images/photos/[^\\\"'<>\\s]+?\\.(?:webp|jpg|jpeg|png)(?:\\?[^\\\"'<>\\s]*)?)",
                    RegexOptions.IgnoreCase);

            List<string> urls =
                new List<string>();

            foreach (Match match in photoRegex.Matches(
                html))
            {
                try
                {
                    string absolute =
                        MakeAbsoluteUrl(
                            WebUtility.HtmlDecode(
                                match.Groups["url"].Value),
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

            return urls;
        }

        private bool IsModHubPhotoUrl(
            string value)
        {
            return value.Contains(
                "/uploads/images/photos/",
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLikelyRealModImage(
            string value)
        {
            string lower =
                value.ToLowerInvariant();

            if (lower.Contains("avatar") ||
                lower.Contains("profile") ||
                lower.Contains("user") ||
                lower.Contains("person") ||
                lower.Contains("default") ||
                lower.Contains("placeholder") ||
                lower.Contains("unknown") ||
                lower.Contains("no-photo") ||
                lower.Contains("no_photo") ||
                lower.Contains("logo") ||
                lower.Contains("icon"))
            {
                return false;
            }

            return
                IsModHubPhotoUrl(value) ||
                lower.Contains("/uploads/") ||
                lower.Contains("/images/mod") ||
                lower.Contains("/mods/");
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
                    "<(?:img|source)\\b[^>]*(?:src|data-src|data-original|data-lazy-src|srcset|data-srcset)=[\\\"'](?<url>[^\\\"']+)[\\\"'][^>]*>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            foreach (Match match in imageRegex.Matches(html))
            {
                string value =
                    WebUtility.HtmlDecode(
                        match.Groups["url"].Value)
                    .Trim();

                if (value.Contains(','))
                {
                    value =
                        value.Split(',')[0]
                            .Trim();
                }

                int descriptorSeparator =
                    value.IndexOf(' ');

                if (descriptorSeparator > 0)
                {
                    value =
                        value.Substring(
                            0,
                            descriptorSeparator);
                }

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
                        @"\.(?:jpg|jpeg|png|webp)(?:\?|$)",
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
