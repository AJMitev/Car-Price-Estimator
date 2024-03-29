// This file was auto-generated by ML.NET Model Builder. 

using Microsoft.ML.Data;

namespace CarPriceEstimatorML.ConsoleApp.DataModels
{
    public class ModelInput
    {
        [ColumnName("FuelType"), LoadColumn(0)]
        public string FuelType { get; set; }


        [ColumnName("GearType"), LoadColumn(1)]
        public string GearType { get; set; }


        [ColumnName("HorsePower"), LoadColumn(2)]
        public float HorsePower { get; set; }


        [ColumnName("Make"), LoadColumn(3)]
        public string Make { get; set; }


        [ColumnName("Model"), LoadColumn(4)]
        public string Model { get; set; }


        [ColumnName("Price"), LoadColumn(5)]
        public float Price { get; set; }


        [ColumnName("Range"), LoadColumn(6)]
        public float Range { get; set; }


        [ColumnName("Year"), LoadColumn(7)]
        public float Year { get; set; }


    }
}
