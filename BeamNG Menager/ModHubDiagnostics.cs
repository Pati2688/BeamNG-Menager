using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace BeamNGModManager
{
    public partial class MainWindow
    {
        private string SaveModHubDownloadDiagnostics(
            string html,
            Uri pageUri)
        {
            StringBuilder report =
                new StringBuilder();

            report.AppendLine(
                "Mods Manager BeamNG - ModHub download diagnostics");

            report.AppendLine(
                "Page: " +
                pageUri);

            report.AppendLine(
                "Time: " +
                DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss"));

            report.AppendLine();

            Regex formRegex =
                new Regex(
                    "<form\\b(?<attrs>[^>]*)>(?<body>.*?)</form>",
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            MatchCollection forms =
                formRegex.Matches(
                    html);

            report.AppendLine(
                "FORMS: " +
                forms.Count);

            for (int i = 0;
                i < forms.Count;
                i++)
            {
                Match form =
                    forms[i];

                report.AppendLine();
                report.AppendLine(
                    "=== FORM " +
                    (i + 1) +
                    " ===");

                report.AppendLine(
                    "ATTRS: " +
                    CleanDiagnosticText(
                        form.Groups["attrs"].Value,
                        1200));

                Regex controlsRegex =
                    new Regex(
                        "<(?:input|button)\\b[^>]*>(?:.*?</button>)?",
                        RegexOptions.IgnoreCase |
                        RegexOptions.Singleline);

                MatchCollection controls =
                    controlsRegex.Matches(
                        form.Groups["body"].Value);

                foreach (Match control
                    in controls)
                {
                    report.AppendLine(
                        "CONTROL: " +
                        CleanDiagnosticText(
                            control.Value,
                            1400));
                }
            }

            report.AppendLine();
            report.AppendLine(
                "=== RELEVANT CONTEXTS ===");

            string[] needles =
            {
                "modsfire",
                "mods.to",
                "mediafire",
                "sharemods",
                "beamngland",
                "download"
            };

            HashSet<string> contexts =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (string needle in needles)
            {
                int index = 0;

                while (true)
                {
                    index =
                        html.IndexOf(
                            needle,
                            index,
                            StringComparison.OrdinalIgnoreCase);

                    if (index < 0)
                    {
                        break;
                    }

                    int start =
                        Math.Max(
                            0,
                            index - 500);

                    int length =
                        Math.Min(
                            html.Length - start,
                            1400);

                    string context =
                        CleanDiagnosticText(
                            html.Substring(
                                start,
                                length),
                            1800);

                    if (contexts.Add(
                        context))
                    {
                        report.AppendLine();
                        report.AppendLine(
                            "--- " +
                            needle +
                            " ---");

                        report.AppendLine(
                            context);
                    }

                    index +=
                        needle.Length;

                    if (contexts.Count >= 30)
                    {
                        break;
                    }
                }

                if (contexts.Count >= 30)
                {
                    break;
                }
            }

            report.AppendLine();
            report.AppendLine(
                "=== URLS ===");

            Regex urlRegex =
                new Regex(
                    @"https?://[^\s\""'<>()]+",
                    RegexOptions.IgnoreCase);

            foreach (string url in urlRegex
                .Matches(html)
                .Cast<Match>()
                .Select(match =>
                    match.Value)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(120))
            {
                report.AppendLine(
                    url);
            }

            string text =
                report.ToString();

            string directory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "BeamNGModManager",
                    "Diagnostics");

            Directory.CreateDirectory(
                directory);

            string file =
                Path.Combine(
                    directory,
                    "modhub-download-debug.txt");

            File.WriteAllText(
                file,
                text,
                Encoding.UTF8);

            try
            {
                Dispatcher.Invoke(() =>
                    Clipboard.SetText(
                        text));
            }
            catch
            {
            }

            return file;
        }

        private string CleanDiagnosticText(
            string value,
            int maxLength)
        {
            string cleaned =
                value
                    .Replace(
                        "\r",
                        " ")
                    .Replace(
                        "\n",
                        " ")
                    .Replace(
                        "\t",
                        " ");

            cleaned =
                Regex.Replace(
                    cleaned,
                    @"\s+",
                    " ")
                .Trim();

            return cleaned.Length <=
                maxLength
                    ? cleaned
                    : cleaned.Substring(
                        0,
                        maxLength) +
                      "...";
        }
    }
}
