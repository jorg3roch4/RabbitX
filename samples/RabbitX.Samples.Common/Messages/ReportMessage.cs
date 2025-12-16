namespace RabbitX.Samples.Common.Messages;

/// <summary>
/// Report generation message.
/// Handler always fails - demonstrates dead letter exchange behavior
/// when retries are exhausted.
/// </summary>
public sealed class ReportMessage
{
    public string ReportId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
    public Dictionary<string, string> Filters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static ReportMessage CreateSample(int index = 1)
    {
        var reportTypes = new[] { "Sales", "Inventory", "Users", "Activity", "Performance" };
        var users = new[] { "admin", "analyst", "manager", "system" };

        return new ReportMessage
        {
            ReportId = $"RPT-{Guid.NewGuid():N}",
            ReportType = reportTypes[Random.Shared.Next(reportTypes.Length)],
            RequestedBy = users[Random.Shared.Next(users.Length)],
            Format = (ReportFormat)Random.Shared.Next(3),
            Filters = new Dictionary<string, string>
            {
                ["date_from"] = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"),
                ["date_to"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                ["limit"] = Random.Shared.Next(100, 10000).ToString()
            },
            CreatedAt = DateTime.UtcNow
        };
    }
}

public enum ReportFormat
{
    Pdf = 0,
    Excel = 1,
    Csv = 2
}
