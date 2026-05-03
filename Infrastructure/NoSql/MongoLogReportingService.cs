using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Domain.Services;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Infrastructure.NoSql;

public class MongoLogReportingService : ILogReportingService
{
    private readonly IMongoCollection<BsonDocument> _col;

    public MongoLogReportingService(MongoLogger logger)
    {
        _col = logger.Logs;
    }

    private static string ToPrettyJson(IEnumerable<BsonDocument> docs)
    {
        var arr = new BsonArray(docs.Select(d => d.DeepClone().AsBsonValue));
        return arr.ToJson(new JsonWriterSettings { Indent = true });
    }

    public async Task<string> SearchLogsAsync(DateTime? from, DateTime? to, string? user, string? level, string? eventType, int limit, CancellationToken cancellationToken = default)
    {
        var f = Builders<BsonDocument>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(user)) f &= Builders<BsonDocument>.Filter.Eq("user", user.Trim());
        if (!string.IsNullOrWhiteSpace(level)) f &= Builders<BsonDocument>.Filter.Eq("level", level.Trim());
        if (!string.IsNullOrWhiteSpace(eventType)) f &= Builders<BsonDocument>.Filter.Eq("eventType", eventType.Trim());
        if (from.HasValue) f &= Builders<BsonDocument>.Filter.Gte("createdAt", from.Value);
        if (to.HasValue) f &= Builders<BsonDocument>.Filter.Lte("createdAt", to.Value);

        var list = await _col.Find(f).SortByDescending(d => d["createdAt"]).Limit(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return ToPrettyJson(list);
    }

    public async Task<string> ReportActivityByPeriodJsonAsync(string period, CancellationToken cancellationToken = default)
    {
        var days = period.ToLowerInvariant() switch
        {
            "day" or "d" => 1,
            "week" or "w" => 7,
            "month" or "m" => 30,
            _ => 7
        };
        var from = DateTime.UtcNow.AddDays(-days);
        var match = Builders<BsonDocument>.Filter.Gte("createdAt", from);

        var list = await _col.Aggregate()
            .Match(match)
            .Group(new BsonDocument
            {
                ["_id"] = new BsonDocument("$dateToString", new BsonDocument { ["format"] = "%Y-%m-%d", ["date"] = "$createdAt" }),
                ["events"] = new BsonDocument("$sum", 1)
            })
            .Sort(new BsonDocument("_id", 1))
            .ToListAsync(cancellationToken);

        return ToPrettyJson(list);
    }

    public async Task<string> ReportTopUsersJsonAsync(int top, CancellationToken cancellationToken = default)
    {
        top = Math.Clamp(top, 1, 100);
        var match = Builders<BsonDocument>.Filter.Exists("user") &
                    Builders<BsonDocument>.Filter.Ne("user", BsonNull.Value) &
                    Builders<BsonDocument>.Filter.Ne("user", string.Empty);

        var list = await _col.Aggregate()
            .Match(match)
            .Group(new BsonDocument { ["_id"] = "$user", ["events"] = new BsonDocument("$sum", 1) })
            .Sort(new BsonDocument("events", -1))
            .Limit(top)
            .ToListAsync(cancellationToken);

        return ToPrettyJson(list);
    }

    public async Task<string> ReportEventTypeDistributionJsonAsync(CancellationToken cancellationToken = default)
    {
        var list = await _col.Aggregate()
            .Group(new BsonDocument
            {
                ["_id"] = new BsonDocument("$ifNull", new BsonArray { "$eventType", "unspecified" }),
                ["count"] = new BsonDocument("$sum", 1)
            })
            .Sort(new BsonDocument("count", -1))
            .ToListAsync(cancellationToken);

        return ToPrettyJson(list);
    }

    public async Task<string> ReportHourlyTrendJsonAsync(int hours, CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, 168);
        var from = DateTime.UtcNow.AddHours(-hours);
        var match = Builders<BsonDocument>.Filter.Gte("createdAt", from);

        var list = await _col.Aggregate()
            .Match(match)
            .Group(new BsonDocument
            {
                ["_id"] = new BsonDocument("$dateToString", new BsonDocument { ["format"] = "%Y-%m-%dT%H:00:00Z", ["date"] = "$createdAt" }),
                ["events"] = new BsonDocument("$sum", 1)
            })
            .Sort(new BsonDocument("_id", 1))
            .ToListAsync(cancellationToken);

        return ToPrettyJson(list);
    }

    public async Task<string> ReportAnomaliesJsonAsync(CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.AddHours(-24);
        var match = Builders<BsonDocument>.Filter.Gte("createdAt", from);
        const int threshold = 30;

        var list = await _col.Aggregate()
            .Match(match)
            .Group(new BsonDocument { ["_id"] = "$user", ["events"] = new BsonDocument("$sum", 1) })
            .Match(new BsonDocument("events", new BsonDocument("$gt", threshold)))
            .Sort(new BsonDocument("events", -1))
            .ToListAsync(cancellationToken);

        var header = new BsonDocument("note", $"Users with more than {threshold} events in the last 24 hours.");
        return ToPrettyJson(new[] { header }.Concat(list));
    }

    public async Task<string> ExportLogsToJsonFileAsync(string filePath, DateTime? from, DateTime? to, string? user, CancellationToken cancellationToken = default)
    {
        var f = Builders<BsonDocument>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(user)) f &= Builders<BsonDocument>.Filter.Eq("user", user.Trim());
        if (from.HasValue) f &= Builders<BsonDocument>.Filter.Gte("createdAt", from.Value);
        if (to.HasValue) f &= Builders<BsonDocument>.Filter.Lte("createdAt", to.Value);

        var list = await _col.Find(f).SortByDescending(d => d["createdAt"]).Limit(5000).ToListAsync(cancellationToken);
        var json = ToPrettyJson(list);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
        return $"Written {list.Count} documents to {filePath}";
    }

    public async Task<string> ExportLogsToCsvFileAsync(string filePath, DateTime? from, DateTime? to, string? user, CancellationToken cancellationToken = default)
    {
        var f = Builders<BsonDocument>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(user)) f &= Builders<BsonDocument>.Filter.Eq("user", user.Trim());
        if (from.HasValue) f &= Builders<BsonDocument>.Filter.Gte("createdAt", from.Value);
        if (to.HasValue) f &= Builders<BsonDocument>.Filter.Lte("createdAt", to.Value);

        var list = await _col.Find(f).SortByDescending(d => d["createdAt"]).Limit(5000).ToListAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("createdAt;level;user;eventType;message;metaJson");
        foreach (var d in list)
        {
            BsonValue metaVal = BsonNull.Value;
            if (d.TryGetValue("meta", out var mv))
                metaVal = mv;
            var meta = (metaVal.ToString() ?? string.Empty).Replace(';', ',');
            sb.AppendLine(string.Join(';', new[]
            {
                EscapeCsv(d.Contains("createdAt") ? d["createdAt"].ToString() ?? "" : ""),
                EscapeCsv(d.Contains("level") ? d["level"].ToString() ?? "" : ""),
                EscapeCsv(d.Contains("user") ? d["user"].ToString() ?? "" : ""),
                EscapeCsv(d.Contains("eventType") ? d["eventType"].ToString() ?? "" : ""),
                EscapeCsv(d.Contains("message") ? d["message"].ToString() ?? "" : ""),
                EscapeCsv(meta)
            }));
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, cancellationToken);
        return $"Written {list.Count} rows to {filePath}";
    }

    private static string EscapeCsv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        var t = s.Replace("\"", "\"\"");
        return $"\"{t}\"";
    }
}
