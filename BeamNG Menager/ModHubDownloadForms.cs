using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BeamNGModManager
{
    public partial class MainWindow
    {
        private async Task<HttpResponseMessage?> TrySubmitModHubDownloadFormAsync(
            string html,
            Uri pageUri)
        {
            Regex formRegex =
                new Regex(
                    "<form\\b(?<attrs>[^>]*)>(?<body>.*?)</form>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            foreach (Match formMatch in formRegex.Matches(html))
            {
                string formHtml =
                    formMatch.Value;

                int contextStart =
                    Math.Max(
                        0,
                        formMatch.Index - 180);

                int contextLength =
                    Math.Min(
                        html.Length - contextStart,
                        formMatch.Length + 420);

                string nearbyContext =
                    html.Substring(
                        contextStart,
                        contextLength);

                string lower =
                    (formHtml +
                     " " +
                     nearbyContext)
                    .ToLowerInvariant();

                bool downloadForm =
                    lower.Contains("modsfire.com") ||
                    lower.Contains("mods.to") ||
                    lower.Contains("mediafire.com") ||
                    lower.Contains("sharemods.com") ||
                    lower.Contains("beamngland.com") ||
                    lower.Contains("direct download");

                if (!downloadForm)
                {
                    continue;
                }

                string attrs =
                    formMatch.Groups["attrs"].Value;

                string action =
                    GetModHubHtmlAttribute(
                        attrs,
                        "action");

                if (string.IsNullOrWhiteSpace(action))
                {
                    action =
                        pageUri.ToString();
                }

                Uri actionUri;

                try
                {
                    actionUri =
                        new Uri(
                            MakeAbsoluteUrl(
                                WebUtility.HtmlDecode(action),
                                pageUri));
                }
                catch
                {
                    continue;
                }

                bool postsBackToCurrentPage =
                    actionUri.Equals(
                        pageUri);

                if (!postsBackToCurrentPage &&
                    IsRejectedDownloadCandidate(
                        actionUri,
                        "ModHub"))
                {
                    continue;
                }

                string method =
                    GetModHubHtmlAttribute(
                        attrs,
                        "method");

                if (string.IsNullOrWhiteSpace(method))
                {
                    method = "get";
                }

                List<KeyValuePair<string, string>> commonFields =
                    new List<KeyValuePair<string, string>>();

                List<KeyValuePair<string, string>> submitFields =
                    new List<KeyValuePair<string, string>>();

                Regex inputRegex =
                    new Regex(
                        "<input\\b(?<attrs>[^>]*)>",
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                foreach (Match inputMatch in inputRegex.Matches(
                    formMatch.Groups["body"].Value))
                {
                    string inputAttrs =
                        inputMatch.Groups["attrs"].Value;

                    string name =
                        GetModHubHtmlAttribute(
                            inputAttrs,
                            "name");

                    string type =
                        GetModHubHtmlAttribute(
                            inputAttrs,
                            "type");

                    string value =
                        WebUtility.HtmlDecode(
                            GetModHubHtmlAttribute(
                                inputAttrs,
                                "value"));

                    if (type.Equals(
                        "file",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (type.Equals(
                        "image",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            submitFields.Add(
                                new KeyValuePair<string, string>(
                                    name + ".x",
                                    "1"));

                            submitFields.Add(
                                new KeyValuePair<string, string>(
                                    name + ".y",
                                    "1"));
                        }

                        continue;
                    }

                    if (type.Equals(
                            "submit",
                            StringComparison.OrdinalIgnoreCase) ||
                        type.Equals(
                            "button",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            submitFields.Add(
                                new KeyValuePair<string, string>(
                                    name,
                                    value));
                        }

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    commonFields.Add(
                        new KeyValuePair<string, string>(
                            name,
                            value));
                }

                Regex buttonRegex =
                    new Regex(
                        "<button\\b(?<attrs>[^>]*)>(?<text>.*?)</button>",
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                foreach (Match buttonMatch in buttonRegex.Matches(
                    formMatch.Groups["body"].Value))
                {
                    string buttonAttrs =
                        buttonMatch.Groups["attrs"].Value;

                    string name =
                        GetModHubHtmlAttribute(
                            buttonAttrs,
                            "name");

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    string value =
                        GetModHubHtmlAttribute(
                            buttonAttrs,
                            "value");

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        value =
                            Regex.Replace(
                                buttonMatch.Groups["text"].Value,
                                "<.*?>",
                                "")
                            .Trim();
                    }

                    submitFields.Add(
                        new KeyValuePair<string, string>(
                            name,
                            WebUtility.HtmlDecode(value)));
                }

                List<List<KeyValuePair<string, string>>> attempts =
                    new List<List<KeyValuePair<string, string>>>();

                if (submitFields.Count == 0)
                {
                    attempts.Add(
                        new List<KeyValuePair<string, string>>(
                            commonFields));
                }
                else
                {
                    HashSet<string> handledImagePrefixes =
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);

                    foreach (KeyValuePair<string, string> submitField
                        in submitFields)
                    {
                        string key =
                            submitField.Key;

                        if (key.EndsWith(
                            ".x",
                            StringComparison.OrdinalIgnoreCase) ||
                            key.EndsWith(
                            ".y",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            string prefix =
                                key.Substring(
                                    0,
                                    key.Length - 2);

                            if (!handledImagePrefixes.Add(prefix))
                            {
                                continue;
                            }

                            List<KeyValuePair<string, string>> imageFields =
                                new List<KeyValuePair<string, string>>(
                                    commonFields);

                            imageFields.Add(
                                new KeyValuePair<string, string>(
                                    prefix + ".x",
                                    "1"));

                            imageFields.Add(
                                new KeyValuePair<string, string>(
                                    prefix + ".y",
                                    "1"));

                            attempts.Add(
                                imageFields);

                            continue;
                        }

                        List<KeyValuePair<string, string>> fields =
                            new List<KeyValuePair<string, string>>(
                                commonFields);

                        fields.Add(
                            submitField);

                        attempts.Add(
                            fields);
                    }
                }

                foreach (List<KeyValuePair<string, string>> fields
                    in attempts)
                {
                    try
                    {
                        using HttpRequestMessage request =
                            BuildModHubFormRequest(
                                actionUri,
                                method,
                                fields);

                        request.Headers.Referrer =
                            pageUri;

                        HttpResponseMessage response =
                            await downloadClient.SendAsync(
                                request,
                                HttpCompletionOption.ResponseHeadersRead);

                        if (!response.IsSuccessStatusCode)
                        {
                            response.Dispose();
                            continue;
                        }

                        string? mediaType =
                            response.Content.Headers
                                .ContentType?
                                .MediaType;

                        string? disposition =
                            response.Content.Headers
                                .ContentDisposition?
                                .DispositionType;

                        bool isFile =
                            (!string.IsNullOrWhiteSpace(disposition) &&
                             disposition.Equals(
                                 "attachment",
                                 StringComparison.OrdinalIgnoreCase)) ||
                            mediaType == "application/zip" ||
                            mediaType == "application/x-zip-compressed" ||
                            mediaType == "application/octet-stream";

                        if (isFile)
                        {
                            return response;
                        }

                        if (mediaType != null &&
                            mediaType.Contains(
                                "html",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            string nextHtml =
                                await response.Content
                                    .ReadAsStringAsync();

                            Uri nextBase =
                                response.RequestMessage?
                                    .RequestUri ??
                                actionUri;

                            Uri? nextUri =
                                ExtractDownloadUriFromPage(
                                    nextHtml,
                                    nextBase,
                                    "ModHub");

                            response.Dispose();

                            if (nextUri != null)
                            {
                                return await GetDownloadResponseAsync(
                                    nextUri,
                                    "ModHub");
                            }

                            continue;
                        }

                        response.Dispose();
                    }
                    catch
                    {
                        // Próbujemy następny przycisk / formularz.
                    }
                }
            }

            return null;
        }

        private HttpRequestMessage BuildModHubFormRequest(
            Uri actionUri,
            string method,
            List<KeyValuePair<string, string>> fields)
        {
            if (method.Equals(
                "post",
                StringComparison.OrdinalIgnoreCase))
            {
                return new HttpRequestMessage(
                    HttpMethod.Post,
                    actionUri)
                {
                    Content =
                        new FormUrlEncodedContent(
                            fields)
                };
            }

            UriBuilder builder =
                new UriBuilder(
                    actionUri);

            string query =
                string.Join(
                    "&",
                    fields.Select(field =>
                        Uri.EscapeDataString(field.Key) +
                        "=" +
                        Uri.EscapeDataString(field.Value)));

            if (!string.IsNullOrWhiteSpace(query))
            {
                string existing =
                    builder.Query.TrimStart('?');

                builder.Query =
                    string.IsNullOrWhiteSpace(existing)
                        ? query
                        : existing + "&" + query;
            }

            return new HttpRequestMessage(
                HttpMethod.Get,
                builder.Uri);
        }

        private string GetModHubHtmlAttribute(
            string attributes,
            string name)
        {
            string pattern =
                "\\b" +
                Regex.Escape(name) +
                "\\s*=\\s*(?:[\\\"'](?<value>.*?)[\\\"']|(?<value>[^\\s>]+))";

            Match match =
                Regex.Match(
                    attributes,
                    pattern,
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            return match.Success
                ? match.Groups["value"].Value
                : "";
        }
    }
}
