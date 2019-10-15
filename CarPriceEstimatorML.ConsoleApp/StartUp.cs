namespace CarPriceEstimatorML.ConsoleApp
{
    using System;
    using System.IO;
    using Microsoft.ML;
    using CarPriceEstimatorML.ConsoleApp.DataModels;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.ML.Trainers.FastTree;
    using CarPriceEstimator.DataGatherer;
    using AngleSharp.Html.Parser;
    using CarPriceEstimator.DataGatherer.Models;
    using CarPriceEstimator.DataGatherer.Utils;

    public class StartUp
    {
        private const string CsvFileName = "cars.csv";
        private const string ModelFileName = "CarModel.zip";

        public static async Task Main()
        {
            if (!File.Exists(CsvFileName))
            {
                await CollectData();
            }

            Console.OutputEncoding = Encoding.UTF8;
            if (!File.Exists(ModelFileName))
            {
                TrainModel(CsvFileName, ModelFileName);
            }

            var listOfInputs = new List<ModelInput>()
            {
                new ModelInput
                {
                    Make = "VW",
                    Model = "Golf",
                    FuelType = "Бензин",
                    GearType = "Ръчни",
                    HorsePower = 55,
                    Range = 270000,
                    Year = 1992
                }
            };

            TestModel(ModelFileName, listOfInputs);
        }

        private static async Task CollectData()
        {
            var dataSources = new List<ICarDataGatherer>
            {
                new CarsBgCarDataGatherer(),
                new CarMarketBgCarDataGatherer()
            };
            using var client = new HttpClient();
            var parser = new HtmlParser();
            var carData = new List<Car>();

            foreach (var source in dataSources)
            {
                var currentSourceData = await source.GatherData(parser, client).ConfigureAwait(false);
                var filteredData = currentSourceData.Where(car => car.HorsePower > 0);

                carData.AddRange(filteredData);
            }

            carData.SaveAsCsv(CsvFileName);
        }

        private static void TrainModel(string dataFile, string modelFile)
        {
            var mlContext = new MLContext(seed: 1);

            // Load Data
            IDataView trainingDataView = mlContext.Data.LoadFromTextFile<ModelInput>(
                                            path: dataFile,
                                            hasHeader: true,
                                            separatorChar: ',',
                                            allowQuoting: true,
                                            allowSparse: false);

            var dataProcessPipeline = mlContext.Transforms.Categorical.OneHotEncoding(new[]
                {
                    new InputOutputColumnPair("FuelType", "FuelType"),
                    new InputOutputColumnPair("GearType", "GearType"),
                    new InputOutputColumnPair("Make", "Make")
                })
                .Append(mlContext.Transforms.Categorical.OneHotHashEncoding(new[] { new InputOutputColumnPair("Model", "Model") }))
                .Append(mlContext.Transforms.Concatenate("Features",
                    new[] { "FuelType", "GearType", "Make", "Model", "HorsePower", "Range", "Year" }));


            var trainer = mlContext.Regression.Trainers.FastTreeTweedie(new FastTreeTweedieTrainer.Options()
            {
                NumberOfLeaves = 85,
                MinimumExampleCountPerLeaf = 1,
                NumberOfTrees = 500,
                LearningRate = 0.07601223f,
                Shrinkage = 0.4191183f,
                LabelColumnName = "Price",
                FeatureColumnName = "Features"
            });

            var trainingPipeline = dataProcessPipeline.Append(trainer);

            mlContext.Regression.CrossValidate(trainingDataView, trainingPipeline, numberOfFolds: 5, labelColumnName: "Price");

            ITransformer model = trainingPipeline.Fit(trainingDataView);

            mlContext.Model.Save(model, trainingDataView.Schema, modelFile);
        }

        private static void TestModel(string modelFilePath, List<ModelInput> listOfInputs)
        {
            var mlContext = new MLContext(1);
            var model = mlContext.Model.Load(modelFilePath, out _);
            var predictEnginePool = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model);

            foreach (var input in listOfInputs)
            {
                var predict = predictEnginePool.Predict(input);


                Console.WriteLine($"{input.Make}, {input.Model} - {predict.Score}");
            }
        }
    }
}
