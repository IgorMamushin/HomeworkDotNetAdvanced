using System.Text.Json.Serialization;
using Models;

namespace Test.Benchmark;

[JsonSerializable(typeof(UserProfile))]
public partial class ModelJsonContext : JsonSerializerContext
{
}