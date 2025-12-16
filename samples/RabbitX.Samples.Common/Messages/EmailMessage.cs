namespace RabbitX.Samples.Common.Messages;

/// <summary>
/// Email sending message.
/// Handler fails randomly (50% chance) - demonstrates retry behavior
/// simulating external service failures.
/// </summary>
public sealed class EmailMessage
{
    public string EmailId { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public EmailType Type { get; set; } = EmailType.Transactional;
    public bool IsHtml { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static EmailMessage CreateSample(int index = 1)
    {
        var domains = new[] { "example.com", "test.org", "sample.net" };
        var subjects = new[]
        {
            "Welcome to our platform",
            "Your weekly summary",
            "Action required",
            "Password reset request",
            "New activity on your account"
        };

        return new EmailMessage
        {
            EmailId = $"EMAIL-{Guid.NewGuid():N}",
            To = $"user{Random.Shared.Next(1000, 9999)}@{domains[Random.Shared.Next(domains.Length)]}",
            Subject = subjects[Random.Shared.Next(subjects.Length)],
            Body = $"<p>This is test email #{index}. Random failures may occur to simulate SMTP issues.</p>",
            Type = (EmailType)Random.Shared.Next(3),
            IsHtml = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}

public enum EmailType
{
    Transactional = 0,
    Marketing = 1,
    Notification = 2
}
