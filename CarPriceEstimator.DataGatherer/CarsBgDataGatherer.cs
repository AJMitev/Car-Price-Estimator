namespace CarPriceEstimator.DataGatherer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;

    public class CarsBgDataGatherer
    {
        public async Task<IEnumerable<Car>> GatherData(int from, int to)
        {
            var cars = new List<Car>();
            var parser = new HtmlParser();
            var client = new HttpClient();

            var makes = await GetMakesWithModels(client, parser);

            int offersCount = 1000;
            int pages = (int)Math.Ceiling(offersCount / 20d);

            for (int page = pages; page >= 1; page--)
            {
                var url = $"https://www.cars.bg/?go=cars&search=1&fromhomeu=1&currencyId=1&autotype=1&stateId=1&offersFor4=1&offersFor1=1&filterOrderBy=1&page={page}&cref={offersCount}";

                try
                {
                    var htmlContent = await GetHtmlContent(client, url);


                    if (string.IsNullOrWhiteSpace(htmlContent))
                    {
                        break;
                    }

                    var document = await parser.ParseDocumentAsync(htmlContent);
                    var carOffersTable = document.GetElementsByClassName("tableListResults").FirstOrDefault();

                    var offersAtCurrentPage = carOffersTable?.Children[0].Children.Length;
                    var offersAtOddPosition = GetOffersLink(document, "odd");
                    var offersAtEvenPosition = GetOffersLink(document, "even");

                    var offersLinks = new List<string>();
                    offersLinks.AddRange(offersAtEvenPosition);
                    offersLinks.AddRange(offersAtOddPosition);

                    for (int i = 0; i < offersAtCurrentPage; i++)
                    {
                        var offerUrl = $"https://www.cars.bg/{offersLinks[i]}";
                        var offerContent = await GetHtmlContent(client, offerUrl);
                        var offerDom = await parser.ParseDocumentAsync(offerContent);

                        var offerTitle =
                            offerDom.QuerySelector("table.ver13black>tbody>tr>td.ver30black.line-bottom-border>strong").InnerHtml;
                        string make = TryGetMake(offerTitle, makes.Keys);
                        string model = TryGetMake(offerTitle.Substring(make.Length), makes[make]);
                    }

                }
                catch (Exception exception)
                {
                    Console.Write(exception);
                }
            }

            return cars;
        }

        private string TryGetMake(string offerTitle, ICollection<string> makesCollection)
        {
            string[] titleParts = offerTitle.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var pattern = string.Empty;

            foreach (var part in titleParts)
            {
                pattern += part;

                if (makesCollection.Contains(pattern))
                {
                    return pattern;
                }

                pattern += " ";
            }

            return null;
        }

        private async Task<Dictionary<string, HashSet<string>>> GetMakesWithModels(HttpClient client, HtmlParser parser)
        {
            var makes = new Dictionary<string, HashSet<string>>();

            var uri =
                "https://www.cars.bg/?go=cars&search=1&advanced=1&fromhomeu=0&publishedTime=0&filterOrderBy=1&showPrice=0&autotype=1&stateId=0&filterOrderBy1=1&brandId=0&modelId=&yearFrom=&yearTo=&priceFrom=&priceTo=&currencyId=1&regionId=0&cityId=0&conditionId=0&photos=0&barter=0&dcredit=0&leasing=0&fuelId=0&gearId=0&usageId=0&steering_weel=0&categoryId=0&offerFrom1=0&offerFrom2=0&offerFrom3=0&offerFrom4=0&offersFor1=0&offersFor2=0&offersFor3=0&offersFor4=0&doorId=0&manual_price=0&man_priceFrom=0&man_priceTo=0";

            var htmlContent = await GetHtmlContent(client, uri);
            var document = await parser.ParseDocumentAsync(htmlContent);
            var makeElement = document.GetElementById("BrandId");
            var optionsGroups = makeElement.Children.Skip(1).ToList();

            for (int i = 0; i < optionsGroups.Count(); i++)
            {
                var currentOptionsGroup = optionsGroups[i];

                for (int j = 0; j < currentOptionsGroup.Children.Length; j++)
                {
                    var currentOption = currentOptionsGroup.Children[j] as IHtmlOptionElement;
                    var currentMake = currentOption?.InnerHtml;

                    if (!makes.ContainsKey(currentMake))
                    {
                        makes.Add(currentMake, new HashSet<string>());
                    }

                    var currentMakeId = currentOption?.Value;

                    var carModelUri = $"https://www.cars.bg/?ajax=multimodel&brandId={currentMakeId}";
                    var carModelHtmlContent = await GetHtmlContent(client, carModelUri);
                    var carModelResult = await parser.ParseDocumentAsync(carModelHtmlContent);

                    var models = carModelResult.GetElementsByClassName("model");
                    foreach (IElement element in models)
                    {
                        var currentModel = element.NextElementSibling.InnerHtml;

                        if (!string.IsNullOrEmpty(currentModel) && !makes[currentMake].Contains(currentModel))
                        {
                            makes[currentMake].Add(currentModel);
                        }
                    }
                }
            }

            return makes;
        }

        private async Task<string> GetHtmlContent(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            var contentBytes = await response.Content.ReadAsByteArrayAsync();
            var htmlContent = System.Text.Encoding.UTF8.GetString(contentBytes);

            return htmlContent;
        }

        private IEnumerable<string> GetOffersLink(IHtmlDocument html, string tag)
        {
            var links = new HashSet<string>();
            var offersAtOddPosition = html.GetElementsByClassName(tag);

            foreach (var element in offersAtOddPosition)
            {
                var pattern = @"offer\/c[0-9]+";
                var link = Regex.Match(element.InnerHtml, pattern);
                links.Add(link.Value);
            }

            return links;
        }
    }
}