namespace CarPriceEstimator.DataGatherer.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public static class HtmlHelpers
    {
        private const string YearPattern = @"[0-9]{4}";
        private const string ThousandsPattern = @"([0-9]+\,[0-9]+)|([0-9]+\s[0-9]+)";

        public static async Task<string> GetHtmlContent(HttpClient client, string url)
        {
            var response = await client.GetAsync(requestUri: url);
            var contentBytes = await response.Content.ReadAsByteArrayAsync();
            var htmlContent = System.Text.Encoding.UTF8.GetString(bytes: contentBytes);

            return htmlContent;
        }

        public static string ParseYear(string yearRaw)
        {
            if (!Regex.IsMatch(yearRaw, YearPattern))
            {
                throw new InvalidOperationException();
            }

            return Regex.Match(input: yearRaw, pattern: YearPattern).Value;
        }

        public static int ParseHorsePower(string rawData)
        {
            var digitAsString = rawData.Split(separator: ' ').First();

            var isDigit = int.TryParse(s: digitAsString, result: out var digit);

            return isDigit ? digit : 0;
        }

        public static int ParseThousands(string rawData, string separator)
        {
            if (!Regex.IsMatch(rawData, ThousandsPattern))
            {
                throw new InvalidOperationException();
            }

            var value = Regex.Match(rawData, ThousandsPattern).Value;
            value = value.Replace(separator, string.Empty);

            return int.Parse(value);
        }

        public static string ParseMakeAndModel(string rawData, ICollection<string> makesCollection)
        {
            var titleParts = rawData.Split(separator: new[] { ' ' }, options: StringSplitOptions.RemoveEmptyEntries);
            var pattern = string.Empty;

            foreach (var part in titleParts)
            {
                pattern += part;

                if (makesCollection.Contains(item: pattern))
                {
                    return pattern;
                }

                pattern += " ";
            }

            return null;
        }
    }
}