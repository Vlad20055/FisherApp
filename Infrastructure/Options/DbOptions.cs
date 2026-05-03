namespace Infrastructure.Options;

public class DbOptions
{
    public string ConnectionString { get; set; } =
        "Host=localhost;Port=5432;Database=fisher;Username=postgres;Password=1111";

    /// <summary>StackExchange.Redis connection string (env FISHER_REDIS).</summary>
    public string RedisConnection { get; set; } =
        Environment.GetEnvironmentVariable("FISHER_REDIS") ?? "localhost,abortConnect=false";

    /// <summary>MongoDB connection string (env FISHER_MONGO).</summary>
    public string MongoConnection { get; set; } =
        Environment.GetEnvironmentVariable("FISHER_MONGO") ?? "mongodb://localhost:27017";
}
