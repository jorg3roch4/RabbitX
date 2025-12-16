namespace RabbitX.Configuration;

/// <summary>
/// RabbitMQ connection configuration.
/// </summary>
public sealed class ConnectionOptions
{
    /// <summary>
    /// The hostname or IP address of the RabbitMQ server.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// The port number for the RabbitMQ server.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// The username for authentication.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// The password for authentication.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// The virtual host to connect to.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Enables automatic connection recovery when the connection is lost.
    /// </summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;

    /// <summary>
    /// The interval between automatic recovery attempts in seconds.
    /// </summary>
    public int NetworkRecoveryIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Heartbeat interval in seconds. Set to 0 to disable.
    /// </summary>
    public int RequestedHeartbeatSeconds { get; set; } = 60;

    /// <summary>
    /// Client-provided connection name for identification in RabbitMQ management console.
    /// </summary>
    public string? ClientProvidedName { get; set; }
}
