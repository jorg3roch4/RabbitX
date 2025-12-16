namespace RabbitX.Configuration;

/// <summary>
/// Root configuration options for RabbitX.
/// </summary>
public sealed class RabbitXOptions
{
    /// <summary>
    /// Connection settings for RabbitMQ.
    /// </summary>
    public ConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Dictionary of publisher configurations keyed by name.
    /// </summary>
    public Dictionary<string, PublisherOptions> Publishers { get; set; } = new();

    /// <summary>
    /// Dictionary of consumer configurations keyed by name.
    /// </summary>
    public Dictionary<string, ConsumerOptions> Consumers { get; set; } = new();

    /// <summary>
    /// Dictionary of RPC client configurations keyed by name.
    /// </summary>
    public Dictionary<string, RpcClientOptions> RpcClients { get; set; } = new();

    /// <summary>
    /// Dictionary of RPC handler configurations keyed by name.
    /// </summary>
    public Dictionary<string, RpcHandlerOptions> RpcHandlers { get; set; } = new();
}
