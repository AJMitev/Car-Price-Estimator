namespace CarPriceEstimator.DataGatherer
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class StartUp
    {
        public static async Task Main()
        {
            var dataSources = new List<ICarDataGatherer>
            {
                new CarsBgCarDataGatherer()
            };

            var carData = new List<Car>();
            foreach (var source in dataSources)
            {
                var currentSourceData = await source.GatherData();
                var filteredData = currentSourceData.Where(car => car.HorsePower > 0);

                carData.AddRange(filteredData);
            }

            carData.SaveAsCSV("./cars.csv");
        }
    }
}