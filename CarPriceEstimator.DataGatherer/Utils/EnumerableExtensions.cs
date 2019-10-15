namespace CarPriceEstimator.DataGatherer.Utils
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;

    public static class EnumerableExtensions
    {
        public static void SaveAsCsv<T>(this IEnumerable<T> items, string path)
        {
            DataValidator.ThrowIfNullOrEmpty(path, nameof(path));
            DataValidator.ThrowIfNull(items, nameof(items));

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