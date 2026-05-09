using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Models;

namespace Test.Benchmark;

[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private readonly UserProfile _userProfile = new()
    {
        Username = "MyUsername",
        Id = 1,
        CreatedAt = DateTime.UtcNow
    };

    [Benchmark(Baseline = true)]
    public byte[] SystemTextJson()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_userProfile, ModelJsonContext.Default.Options);
    }

    [Benchmark]
    public byte[] SourceGenerator()
    {
        return _userProfile.Serialize();
    }
}