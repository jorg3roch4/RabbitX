namespace RabbitX.Sample.Rpc.Messages;

/// <summary>
/// Request to perform a calculation.
/// </summary>
public sealed class CalculateRequest
{
    public double A { get; set; }
    public double B { get; set; }
    public string Operation { get; set; } = "add";
}

/// <summary>
/// Response with the calculation result.
/// </summary>
public sealed class CalculateResponse
{
    public double Result { get; set; }
    public string? Error { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Request for user information.
/// </summary>
public sealed class GetUserRequest
{
    public int UserId { get; set; }
}

/// <summary>
/// Response with user information.
/// </summary>
public sealed class GetUserResponse
{
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
