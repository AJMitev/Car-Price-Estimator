namespace CarPriceEstimator.DataGatherer.Utils
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    public static class EnumerableExtensions
    {
        public static void SaveAsCsv<T>(this IEnumerable<T> items, string path)
        {
            var itemType = typeof(T);
            var props = itemType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p => p.Name);

            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            writer.WriteLine(string.Join(", ", props.Select(p => p.Name)));

            foreach (var item in items)
            {
                writer.WriteLine(string.Join(", ", props.Select(p => p.GetValue(item, null))));
            }
        }
    }
}