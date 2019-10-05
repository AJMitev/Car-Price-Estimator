namespace CarPriceEstimator.DataGatherer.Models
{
    public class Car
    {
        public string Make { get; set; }
        public string Model { get; set; }
        public string FuelType { get; set; }
        public string GearType { get; set; }
        public int HorsePower { get; set; }
        public int Range { get; set; }
        public string Year { get; set; }
        public int Price { get; set; }
    }
}