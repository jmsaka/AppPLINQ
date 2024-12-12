using System.Diagnostics;
using System.Collections.Concurrent;

namespace AppPLINQ;

class Program
{
    static void Main(string[] args)
    {
        var largeData = GenerateLargeData(10_000);

        Console.WriteLine($"Iniciando com {largeData.Count} registros");
        Console.WriteLine("");

        var stopwatch = new Stopwatch();

        // LINQ tradicional
        stopwatch.Start();
        var resultLinq = largeData
            .Where(x => x.Value % 2 == 0)
            .Select(x => new { x.Id, ComputedValue = ExpensiveComputation(x.Value) })
            .ToList();
        stopwatch.Stop();
        Console.WriteLine($"Total (resultLinq): {resultLinq.Count}");
        Console.WriteLine($"LINQ tradicional levou: {stopwatch.ElapsedMilliseconds} ms\n");

        // PLINQ
        stopwatch.Restart();
        var resultPlinq = largeData
            .AsParallel()
            .Where(x => x.Value % 2 == 0)
            .Select(x => new { x.Id, ComputedValue = ExpensiveComputation(x.Value) })
            .ToList();
        stopwatch.Stop();
        Console.WriteLine($"Total (resultPlinq): {resultPlinq.Count}");
        Console.WriteLine($"PLINQ levou: {stopwatch.ElapsedMilliseconds} ms\n");

        // Parallel.ForEach com ConcurrentBag
        stopwatch.Restart();
        var resultParallelForEach = new ConcurrentBag<object>();
        Parallel.ForEach(largeData, item =>
        {
            if (item.Value % 2 == 0)
            {
                var computed = new { item.Id, ComputedValue = ExpensiveComputation(item.Value) };
                resultParallelForEach.Add(computed);
            }
        });
        stopwatch.Stop();
        Console.WriteLine($"Total (resultParallelForEach): {resultParallelForEach.Count}");
        Console.WriteLine($"Parallel.ForEach levou: {stopwatch.ElapsedMilliseconds} ms\n");

        // System.Threading.Tasks com partições manuais
        stopwatch.Restart();
        int partitionSize = largeData.Count / Environment.ProcessorCount;
        var tasks = Enumerable.Range(0, Environment.ProcessorCount).Select(partition => Task.Run(() =>
        {
            var localResults = new List<object>();
            var start = partition * partitionSize;
            var end = (partition == Environment.ProcessorCount - 1) ? largeData.Count : start + partitionSize;

            for (int i = start; i < end; i++)
            {
                if (largeData[i].Value % 2 == 0)
                {
                    localResults.Add(new { largeData[i].Id, ComputedValue = ExpensiveComputation(largeData[i].Value) });
                }
            }

            return localResults;
        })).ToArray();

        Task.WaitAll(tasks);
        var resultPartitionedTasks = tasks.SelectMany(t => t.Result).ToList();
        stopwatch.Stop();
        Console.WriteLine($"Total (resultPartitionedTasks): {resultPartitionedTasks.Count}");
        Console.WriteLine($"Tasks com partições levou: {stopwatch.ElapsedMilliseconds} ms\n");

        Console.WriteLine("Finalizando");
    }

    static List<DataItem> GenerateLargeData(int count)
    {
        var data = new List<DataItem>(count);
        var random = new Random();
        for (int i = 0; i < count; i++)
        {
            data.Add(new DataItem { Id = i, Value = random.Next(1, 1_000_000) });
        }
        return data;
    }

    static int ExpensiveComputation(int value)
    {
        Task.Delay(1).Wait(); // Simula uma operação custosa
        return value * value;
    }

    class DataItem
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }
}
