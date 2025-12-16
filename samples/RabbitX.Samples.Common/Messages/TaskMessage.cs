namespace RabbitX.Samples.Common.Messages;

/// <summary>
/// Background task processing message.
/// Handler always succeeds - demonstrates successful processing flow.
/// </summary>
public sealed class TaskMessage
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static TaskMessage CreateSample(int index = 1)
    {
        var taskTypes = new[] { "DataSync", "Cleanup", "Export", "Import", "Transform" };

        return new TaskMessage
        {
            TaskId = $"TASK-{Guid.NewGuid():N}",
            TaskType = taskTypes[Random.Shared.Next(taskTypes.Length)],
            Description = $"Background task #{index}",
            Priority = (TaskPriority)Random.Shared.Next(3),
            Parameters = new Dictionary<string, string>
            {
                ["source"] = $"system-{Random.Shared.Next(1, 10)}",
                ["batch_size"] = Random.Shared.Next(100, 1000).ToString()
            },
            CreatedAt = DateTime.UtcNow
        };
    }
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}
