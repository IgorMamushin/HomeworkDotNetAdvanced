using System.Diagnostics;

namespace Models.Observability;

public class ServiceTrace
{
    public static readonly ActivitySource ActivitySource = new("CacheService", "0.0.1");
}