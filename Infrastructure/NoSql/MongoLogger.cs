using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Generic;

namespace Infrastructure.NoSql;

public class MongoLogger
{
    private readonly IMongoCollection<BsonDocument> _col;

    public IMongoCollection<BsonDocument> Logs => _col;

    public MongoLogger(string connectionString, string dbName = "fisher_logs", string collection = "logs")
    {
        var client = new MongoClient(connectionString);
        var db = client.GetDatabase(dbName);
        _col = db.GetCollection<BsonDocument>(collection);

        try
        {
            var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("createdAt");
            var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(30), Name = "createdAt_ttl" };
            _col.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(indexKeys, indexOptions));
        }
        catch (MongoCommandException)
        {
            // index already exists or incompatible — ignore on startup
        }

        try
        {
            var userTime = Builders<BsonDocument>.IndexKeys.Ascending("user").Ascending("createdAt");
            _col.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(userTime, new CreateIndexOptions { Name = "user_createdAt" }));
        }
        catch (MongoCommandException)
        {
        }
    }

    public Task LogAsync(string level, string user, string message, BsonDocument? meta = null, string? eventType = null)
    {
        var doc = new BsonDocument
        {
            ["level"] = level,
            ["user"] = user ?? string.Empty,
            ["message"] = message,
            ["meta"] = meta ?? new BsonDocument(),
            ["createdAt"] = DateTime.UtcNow
        };
        if (!string.IsNullOrEmpty(eventType))
            doc["eventType"] = eventType;

        return _col.InsertOneAsync(doc);
    }

    public async Task<BsonArray> QueryAsync(FilterDefinition<BsonDocument> filter, int limit = 100)
    {
        var cursor = await _col.FindAsync(filter, new FindOptions<BsonDocument> { Limit = limit });
        var list = await cursor.ToListAsync();
        var arr = new BsonArray();
        foreach (var d in list) arr.Add(d);
        return arr;
    }

    public async Task<BsonArray> AggregateAsync(PipelineDefinition<BsonDocument, BsonDocument> pipeline)
    {
        var cursor = await _col.AggregateAsync(pipeline);
        var list = await cursor.ToListAsync();
        var arr = new BsonArray();
        foreach (var d in list) arr.Add(d);
        return arr;
    }

    public async Task<BsonArray> FindByUserAndPeriodAsync(string? user, DateTime? from, DateTime? to, string? level = null, string? eventType = null, int limit = 100)
    {
        var builder = Builders<BsonDocument>.Filter;
        var filter = builder.Empty;
        if (!string.IsNullOrEmpty(user)) filter = filter & builder.Eq("user", user);
        if (from.HasValue) filter = filter & builder.Gte("createdAt", from.Value);
        if (to.HasValue) filter = filter & builder.Lte("createdAt", to.Value);
        if (!string.IsNullOrEmpty(level)) filter = filter & builder.Eq("level", level);
        if (!string.IsNullOrEmpty(eventType)) filter = filter & builder.Eq("eventType", eventType);
        return await QueryAsync(filter, limit);
    }
}
