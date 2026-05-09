using BenchmarkDotNet.Running;

namespace Test.Benchmark;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SerializationBenchmarks>();
        BenchmarkRunner.Run<DeserializationBenchmarks>();
    }
}