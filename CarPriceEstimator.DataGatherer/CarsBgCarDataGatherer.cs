namespace CarPriceEstimator.DataGatherer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using AngleSharp.Dom;
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;

    public class CarsBgDataGatherer : IDataGatherer
    {
        private const string AdvancedSearchUri = "https://www.cars.bg/?go=cars&search=1&advanced=1&fromhomeu=0&publishedTime=0&filterOrderBy=1&showPrice=0&autotype=1&stateId=0&filterOrderBy1=1&brandId=0&modelId=&yearFrom=&yearTo=&priceFrom=&priceTo=&currencyId=1&regionId=0&cityId=0&conditionId=0&photos=0&barter=0&dcredit=0&leasing=0&fuelId=0&gearId=0&usageId=0&steering_weel=0&categoryId=0&offerFrom1=0&offerFrom2=0&offerFrom3=0&offerFrom4=0&offersFor1=0&offersFor2=0&offersFor3=0&offersFor4=0&doorId=0&manual_price=0&man_priceFrom=0&man_priceTo=0";
        private const string CarOfferPattern = @"offer\/c[0-9]+";
        private const string YearPattern = @"[0-9]{4}";


        public async Task<IEnumerable<Car>> GatherData(int offersCount, int offersPerPage = 20)
        {
            var cars = new List<Car>();
            var parser = new HtmlParser();
            var client = new HttpClient();

            var makes = await GetMakesWithModels(client: client, parser: parser);
            var pages = (int)Math.Ceiling(a: offersCount / (double)offersPerPage);

            for (int page = pages; page >= 1; page--)
            {
                var url = $"https://www.cars.bg/?go=cars&search=1&fromhomeu=1&currencyId=1&autotype=1&stateId=1&offersFor4=1&offersFor1=1&filterOrderBy=1&page={page}&cref={offersCount}";

                try
                {
                    var htmlContent = await GetHtmlContent(client: client, url: url);

                    if (string.IsNullOrWhiteSpace(value: htmlContent))
                    {
                        break;
                    }

                    var document = await parser.ParseDocumentAsync(source: htmlContent);
                    var carOffersTable = document.GetElementsByClassName(classNames: "tableListResults").FirstOrDefault();

                    var offersAtCurrentPage = carOffersTable?.Children[index: 0].Children.Length;
                    var offersLinks = GetOffersUri(document: document);

                    for (int i = 0; i < offersAtCurrentPage - 1; i++)
                    {
                        var offerUrl = $"https://www.cars.bg/{offersLinks[index: i]}";
                        Console.WriteLine(value: $"Crawling from {offerUrl}");
                        var offerContent = await GetHtmlContent(client: client, url: offerUrl);
                        var offerDom = await parser.ParseDocumentAsync(source: offerContent);

                        var offerTitle =
                            offerDom.QuerySelector(selectors: "table.ver13black>tbody>tr>td.ver30black.line-bottom-border>strong").InnerHtml;
                        var make = TryGetMake(offerTitle: offerTitle, makesCollection: makes.Keys);
                        var model = TryGetMake(offerTitle: offerTitle.Substring(startIndex: make.Length), makesCollection: makes[key: make]);

                        var offerDetailsTable = offerDom.QuerySelector(selectors: "table.ver13black").Children[index: 0].Children[index: 2];
                        var offerDetailsLeftRows = offerDetailsTable.Children[index: 0].Children[index: 0].Children[index: 0].Children[index: 0].Children[index: 0].Children[index: 0].Children[index: 0];
                        var offerDetailsRightRows = offerDetailsTable.Children[index: 0].Children[index: 0].Children[index: 0].Children[index: 0].Children[index: 1].Children[index: 0].Children[index: 0];

                        var yearString = offerDetailsLeftRows?.Children[index: 0]?.Children[index: 1]?.InnerHtml;
                        var rangeString = offerDetailsLeftRows?.Children[index: 1]?.Children[index: 1]?.InnerHtml;
                        var fuelType = offerDetailsLeftRows?.Children[index: 2]?.Children[index: 1]?.InnerHtml;
                        var gearType = offerDetailsLeftRows?.Children[index: 3]?.Children[index: 1]?.InnerHtml;
                        var horsePowerString = offerDetailsRightRows?.Children[index: 0]?.Children[index: 1]?.InnerHtml;
                        var cubicCapacityString = offerDetailsRightRows?.Children[index: 1]?.Children[index: 1]?.InnerHtml;

                        var year = GetYear(yearString: yearString);
                        var range = GetRange(rangeString: rangeString);
                        var cubicCapacity = GetDigitFromString(valueAsString: cubicCapacityString);
                        var horsePower = GetDigitFromString(valueAsString: horsePowerString);

                        var currentCar = new Car
                        {
                            Make = make,
                            Model = model,
                            Year = year,
                            Range = range,
                            FuelType = fuelType,
                            GearType = gearType,
                            CubicCapacity = cubicCapacity,
                            HorsePower = horsePower
                        };

                        cars.Add(item: currentCar);
                    }

                }
                catch
                {
                    // If any data is not available just continue 
                }
            }

            return cars;
        }

        private List<string> GetOffersUri(IHtmlDocument document)
        {
            var offersAtOddPosition = GetOffersLink(html: document, tag: "odd");
            var offersAtEvenPosition = GetOffersLink(html: document, tag: "even");

            var offersLinks = new List<string>();
            offersLinks.AddRange(collection: offersAtEvenPosition);
            offersLinks.AddRange(collection: offersAtOddPosition);
            return offersLinks;
        }

        private int GetDigitFromString(string valueAsString)
        {
            var digitAsString = valueAsString.Split(separator: " ")
                .First();

            var isDigit = int.TryParse(s: digitAsString, result: out var digit);

            return isDigit ? digit : 0;
        }

        private int GetRange(string rangeString)
        {
            var digitAsString = rangeString.Replace(oldValue: ",", newValue: string.Empty)
                .Split(separator: " ")
                .First();

            return GetDigitFromString(valueAsString: digitAsString);
        }

        private string GetYear(string yearString)
        {
            return Regex.Match(input: yearString, pattern: YearPattern).Value;
        }

        private string TryGetMake(string offerTitle, ICollection<string> makesCollection)
        {
            var titleParts = offerTitle.Split(separator: new[] { ' ' }, options: StringSplitOptions.RemoveEmptyEntries);
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

        private async Task<Dictionary<string, HashSet<string>>> GetMakesWithModels(HttpClient client, HtmlParser parser)
        {
            var makes = new Dictionary<string, HashSet<string>>();


            var htmlContent = await GetHtmlContent(client: client, url: AdvancedSearchUri);
            var document = await parser.ParseDocumentAsync(source: htmlContent);
            var makeElement = document.GetElementById(elementId: "BrandId");
            var optionsGroups = makeElement.Children.Skip(count: 1).ToList();

            for (int i = 0; i < optionsGroups.Count(); i++)
            {
                var currentOptionsGroup = optionsGroups[index: i];

                for (int j = 0; j < currentOptionsGroup.Children.Length; j++)
                {
                    var currentOption = currentOptionsGroup.Children[index: j] as IHtmlOptionElement;
                    var currentMake = currentOption?.InnerHtml;

                    if (!makes.ContainsKey(key: currentMake))
                    {
                        makes.Add(key: currentMake, value: new HashSet<string>());
                    }

                    var currentMakeId = currentOption?.Value;

                    var carModelUri = $"https://www.cars.bg/?ajax=multimodel&brandId={currentMakeId}";
                    var carModelHtmlContent = await GetHtmlContent(client: client, url: carModelUri);
                    var carModelResult = await parser.ParseDocumentAsync(source: carModelHtmlContent);

                    var models = carModelResult.GetElementsByClassName(classNames: "model");
                    foreach (IElement element in models)
                    {
                        var currentModel = element.NextElementSibling.InnerHtml;

                        if (!string.IsNullOrEmpty(value: currentModel) && !makes[key: currentMake].Contains(item: currentModel))
                        {
                            makes[key: currentMake].Add(item: currentModel);
                        }
                    }
                }
            }

            return makes;
        }

        private async Task<string> GetHtmlContent(HttpClient client, string url)
        {
            var response = await client.GetAsync(requestUri: url);
            var contentBytes = await response.Content.ReadAsByteArrayAsync();
            var htmlContent = System.Text.Encoding.UTF8.GetString(bytes: contentBytes);

            return htmlContent;
        }

        private IEnumerable<string> GetOffersLink(IHtmlDocument html, string tag)
        {
            var links = new HashSet<string>();
            var offersAtOddPosition = html.GetElementsByClassName(classNames: tag);

            foreach (var element in offersAtOddPosition)
            {
                var link = Regex.Match(input: element.InnerHtml, pattern: CarOfferPattern);
                links.Add(item: link.Value);
            }

            return links;
        }
    }
}