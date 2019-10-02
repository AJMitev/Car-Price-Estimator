namespace CarPriceEstimator.DataGatherer
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ICarDataGatherer
    {
        Task<IEnumerable<Car>> GatherData();
    }
}