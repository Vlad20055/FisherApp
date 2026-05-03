namespace Domain.Services;

/// <summary>Отчёты и выборки по журналу MongoDB (агрегации, экспорт).</summary>
public interface ILogReportingService
{
    Task<string> SearchLogsAsync(DateTime? from, DateTime? to, string? user, string? level, string? eventType, int limit, CancellationToken cancellationToken = default);

    Task<string> ReportActivityByPeriodJsonAsync(string period, CancellationToken cancellationToken = default);

    Task<string> ReportTopUsersJsonAsync(int top, CancellationToken cancellationToken = default);

    Task<string> ReportEventTypeDistributionJsonAsync(CancellationToken cancellationToken = default);

    Task<string> ReportHourlyTrendJsonAsync(int hours, CancellationToken cancellationToken = default);

    Task<string> ReportAnomaliesJsonAsync(CancellationToken cancellationToken = default);

    Task<string> ExportLogsToJsonFileAsync(string filePath, DateTime? from, DateTime? to, string? user, CancellationToken cancellationToken = default);

    Task<string> ExportLogsToCsvFileAsync(string filePath, DateTime? from, DateTime? to, string? user, CancellationToken cancellationToken = default);
}
