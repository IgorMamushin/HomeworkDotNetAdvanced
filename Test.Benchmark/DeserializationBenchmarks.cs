using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Models;

namespace Test.Benchmark;

[MemoryDiagnoser]
public class DeserializationBenchmarks
{
    private byte[] _jsonUserProfile = null!;
    private byte[] _byteUserProfile = null!;

    [GlobalSetup]
    public void Setup()
    {
        var userProfile = new UserProfile
        {
            Username = "MyUsername",
            Id = 1,
            CreatedAt = DateTime.UtcNow
        };

        _byteUserProfile = userProfile.Serialize();
        _jsonUserProfile = JsonSerializer.SerializeToUtf8Bytes(userProfile, ModelJsonContext.Default.Options);
    }

    [Benchmark(Baseline = true)]
    public UserProfile? SystemTextJson()
    {
        return JsonSerializer.Deserialize<UserProfile>(_jsonUserProfile, ModelJsonContext.Default.Options);
    }

    [Benchmark]
    public UserProfile? SourceGenerator()
    {
        return UserProfile.Deserialize(_byteUserProfile);
    }
}