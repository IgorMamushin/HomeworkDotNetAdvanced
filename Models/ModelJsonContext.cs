using System.Text.Json.Serialization;

namespace Models;

[JsonSerializable(typeof(UserProfile))]
public partial class ModelJsonContext : JsonSerializerContext
{
}