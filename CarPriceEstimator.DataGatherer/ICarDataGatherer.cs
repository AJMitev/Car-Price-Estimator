namespace CarPriceEstimator.DataGatherer
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using AngleSharp.Html.Parser;
    using CarPriceEstimator.DataGatherer.Models;

    public interface ICarDataGatherer
    {
        Task<IEnumerable<Car>> GatherData(HtmlParser parser, HttpClient client);
    }
}