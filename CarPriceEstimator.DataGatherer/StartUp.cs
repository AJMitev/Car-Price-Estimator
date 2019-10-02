namespace CarPriceEstimator.DataGatherer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AngleSharp.Html.Parser;

    public class StartUp
    {
        public static async Task Main()
        {
            var dataSources = new List<ICarDataGatherer>
            {
                //new CarsBgCarDataGatherer(),
                new CarMarketBgCarDataGatherer()
            };


            using var client = new HttpClient();
            var parser = new HtmlParser();
            var carData = new List<Car>();
            foreach (var source in dataSources)
            {
                var currentSourceData = await source.GatherData(parser, client);
                var filteredData = currentSourceData.Where(car => car.HorsePower > 0);

                carData.AddRange(filteredData);
            }

            carData.SaveAsCSV("./cars.csv");
        }
    }
}