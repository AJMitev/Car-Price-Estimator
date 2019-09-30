namespace CarPriceEstimator.DataGatherer
{
    using System.Text;

    public class Program
    {
        public static void Main()
        {
            System.Text.EncodingProvider provider = System.Text.CodePagesEncodingProvider.Instance;
            Encoding.RegisterProvider(provider);

            var comments = new CarsBgDataGatherer().GatherData(415500, 1).GetAwaiter().GetResult();
        }
    }
}