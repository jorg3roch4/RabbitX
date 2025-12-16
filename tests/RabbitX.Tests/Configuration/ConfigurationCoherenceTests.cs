using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RabbitX.Builders;
using RabbitX.Configuration;
using System.Reflection;
using Xunit;

namespace RabbitX.Tests.Configuration;

/// <summary>
/// Tests to verify coherence between Fluent API and appsettings.json configuration.
/// </summary>
public class ConfigurationCoherenceTests
{
    #region Connection Configuration Tests

    [Fact]
    public void ConnectionOptions_AllProperties_CanBeSetViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Connection"": {
                    ""HostName"": ""testhost"",
                    ""Port"": 5673,
                    ""UserName"": ""testuser"",
                    ""Password"": ""testpass"",
                    ""VirtualHost"": ""/test"",
                    ""AutomaticRecoveryEnabled"": false,
                    ""NetworkRecoveryIntervalSeconds"": 30,
                    ""ConnectionTimeoutSeconds"": 60,
                    ""RequestedHeartbeatSeconds"": 120,
                    ""ClientProvidedName"": ""TestClient""
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        options.Connection.HostName.Should().Be("testhost");
        options.Connection.Port.Should().Be(5673);
        options.Connection.UserName.Should().Be("testuser");
        options.Connection.Password.Should().Be("testpass");
        options.Connection.VirtualHost.Should().Be("/test");
        options.Connection.AutomaticRecoveryEnabled.Should().BeFalse();
        options.Connection.NetworkRecoveryIntervalSeconds.Should().Be(30);
        options.Connection.ConnectionTimeoutSeconds.Should().Be(60);
        options.Connection.RequestedHeartbeatSeconds.Should().Be(120);
        options.Connection.ClientProvidedName.Should().Be("TestClient");
    }

    [Fact]
    public void ConnectionOptions_FluentApi_MatchesAppSettings()
    {
        // Arrange - Fluent API
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("testhost", 5673)
            .UseCredentials("testuser", "testpass")
            .UseVirtualHost("/test")
            .DisableAutoRecovery()
            .WithConnectionTimeout(TimeSpan.FromSeconds(60))
            .WithHeartbeat(TimeSpan.FromSeconds(120))
            .WithClientName("TestClient")
            .AddPublisher("Test", p => p.ToExchange("test.exchange"));

        var fluentOptions = GetOptions(builder);

        // Arrange - AppSettings
        var json = @"{
            ""RabbitX"": {
                ""Connection"": {
                    ""HostName"": ""testhost"",
                    ""Port"": 5673,
                    ""UserName"": ""testuser"",
                    ""Password"": ""testpass"",
                    ""VirtualHost"": ""/test"",
                    ""AutomaticRecoveryEnabled"": false,
                    ""ConnectionTimeoutSeconds"": 60,
                    ""RequestedHeartbeatSeconds"": 120,
                    ""ClientProvidedName"": ""TestClient""
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var appSettingsOptions = new RabbitXOptions();
        configuration.GetSection("RabbitX").Bind(appSettingsOptions);

        // Assert
        appSettingsOptions.Connection.HostName.Should().Be(fluentOptions.Connection.HostName);
        appSettingsOptions.Connection.Port.Should().Be(fluentOptions.Connection.Port);
        appSettingsOptions.Connection.UserName.Should().Be(fluentOptions.Connection.UserName);
        appSettingsOptions.Connection.Password.Should().Be(fluentOptions.Connection.Password);
        appSettingsOptions.Connection.VirtualHost.Should().Be(fluentOptions.Connection.VirtualHost);
        appSettingsOptions.Connection.AutomaticRecoveryEnabled.Should().Be(fluentOptions.Connection.AutomaticRecoveryEnabled);
        appSettingsOptions.Connection.ConnectionTimeoutSeconds.Should().Be(fluentOptions.Connection.ConnectionTimeoutSeconds);
        appSettingsOptions.Connection.RequestedHeartbeatSeconds.Should().Be(fluentOptions.Connection.RequestedHeartbeatSeconds);
        appSettingsOptions.Connection.ClientProvidedName.Should().Be(fluentOptions.Connection.ClientProvidedName);
    }

    #endregion

    #region Publisher Configuration Tests

    [Fact]
    public void PublisherOptions_AllProperties_CanBeSetViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Publishers"": {
                    ""TestPublisher"": {
                        ""Exchange"": ""test.exchange"",
                        ""ExchangeType"": ""topic"",
                        ""RoutingKey"": ""test.key"",
                        ""Durable"": false,
                        ""AutoDelete"": true,
                        ""Persistent"": false,
                        ""Mandatory"": true,
                        ""ConfirmTimeoutMs"": 10000
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        options.Publishers.Should().ContainKey("TestPublisher");
        var publisher = options.Publishers["TestPublisher"];
        publisher.Exchange.Should().Be("test.exchange");
        publisher.ExchangeType.Should().Be("topic");
        publisher.RoutingKey.Should().Be("test.key");
        publisher.Durable.Should().BeFalse();
        publisher.AutoDelete.Should().BeTrue();
        publisher.Persistent.Should().BeFalse();
        publisher.Mandatory.Should().BeTrue();
        publisher.ConfirmTimeoutMs.Should().Be(10000);
    }

    [Fact]
    public void PublisherOptions_FluentApi_MatchesAppSettings()
    {
        // Arrange - Fluent API
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddPublisher("TestPublisher", p => p
                .ToExchange("test.exchange", "topic")
                .WithRoutingKey("test.key")
                .Durable(false)
                .AutoDelete()
                .Persistent(false)
                .Mandatory()
                .WithConfirmTimeout(TimeSpan.FromSeconds(10)));

        var fluentOptions = GetOptions(builder);

        // Arrange - AppSettings
        var json = @"{
            ""RabbitX"": {
                ""Publishers"": {
                    ""TestPublisher"": {
                        ""Exchange"": ""test.exchange"",
                        ""ExchangeType"": ""topic"",
                        ""RoutingKey"": ""test.key"",
                        ""Durable"": false,
                        ""AutoDelete"": true,
                        ""Persistent"": false,
                        ""Mandatory"": true,
                        ""ConfirmTimeoutMs"": 10000
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var appSettingsOptions = new RabbitXOptions();
        configuration.GetSection("RabbitX").Bind(appSettingsOptions);

        // Assert
        var fluentPub = fluentOptions.Publishers["TestPublisher"];
        var appPub = appSettingsOptions.Publishers["TestPublisher"];

        appPub.Exchange.Should().Be(fluentPub.Exchange);
        appPub.ExchangeType.Should().Be(fluentPub.ExchangeType);
        appPub.RoutingKey.Should().Be(fluentPub.RoutingKey);
        appPub.Durable.Should().Be(fluentPub.Durable);
        appPub.AutoDelete.Should().Be(fluentPub.AutoDelete);
        appPub.Persistent.Should().Be(fluentPub.Persistent);
        appPub.Mandatory.Should().Be(fluentPub.Mandatory);
        appPub.ConfirmTimeoutMs.Should().Be(fluentPub.ConfirmTimeoutMs);
    }

    #endregion

    #region Consumer Configuration Tests

    [Fact]
    public void ConsumerOptions_AllProperties_CanBeSetViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Consumers"": {
                    ""TestConsumer"": {
                        ""Queue"": ""test.queue"",
                        ""Exchange"": ""test.exchange"",
                        ""ExchangeType"": ""topic"",
                        ""RoutingKey"": ""test.#"",
                        ""Durable"": false,
                        ""Exclusive"": true,
                        ""AutoDelete"": true,
                        ""MessageTtlSeconds"": 3600,
                        ""Qos"": {
                            ""PrefetchSize"": 1024,
                            ""PrefetchCount"": 20,
                            ""Global"": true
                        },
                        ""Retry"": {
                            ""MaxRetries"": 3,
                            ""DelaysInSeconds"": [5, 10, 30],
                            ""InitialDelaySeconds"": 2,
                            ""MaxDelaySeconds"": 120,
                            ""BackoffMultiplier"": 3.0,
                            ""UseJitter"": false,
                            ""OnRetryExhausted"": ""Discard""
                        },
                        ""DeadLetter"": {
                            ""Exchange"": ""test.dlx"",
                            ""RoutingKey"": ""test.failed"",
                            ""Queue"": ""test.dlq"",
                            ""ExchangeType"": ""fanout"",
                            ""Durable"": false
                        }
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        options.Consumers.Should().ContainKey("TestConsumer");
        var consumer = options.Consumers["TestConsumer"];

        // Basic properties
        consumer.Queue.Should().Be("test.queue");
        consumer.Exchange.Should().Be("test.exchange");
        consumer.ExchangeType.Should().Be("topic");
        consumer.RoutingKey.Should().Be("test.#");
        consumer.Durable.Should().BeFalse();
        consumer.Exclusive.Should().BeTrue();
        consumer.AutoDelete.Should().BeTrue();
        consumer.MessageTtlSeconds.Should().Be(3600);

        // QoS
        consumer.Qos.PrefetchSize.Should().Be(1024);
        consumer.Qos.PrefetchCount.Should().Be(20);
        consumer.Qos.Global.Should().BeTrue();

        // Retry
        consumer.Retry.MaxRetries.Should().Be(3);
        consumer.Retry.DelaysInSeconds.Should().BeEquivalentTo(new[] { 5, 10, 30 });
        consumer.Retry.InitialDelaySeconds.Should().Be(2);
        consumer.Retry.MaxDelaySeconds.Should().Be(120);
        consumer.Retry.BackoffMultiplier.Should().Be(3.0);
        consumer.Retry.UseJitter.Should().BeFalse();
        consumer.Retry.OnRetryExhausted.Should().Be(RetryExhaustedAction.Discard);

        // DeadLetter
        consumer.DeadLetter.Should().NotBeNull();
        consumer.DeadLetter!.Exchange.Should().Be("test.dlx");
        consumer.DeadLetter.RoutingKey.Should().Be("test.failed");
        consumer.DeadLetter.Queue.Should().Be("test.dlq");
        consumer.DeadLetter.ExchangeType.Should().Be("fanout");
        consumer.DeadLetter.Durable.Should().BeFalse();
    }

    [Fact]
    public void ConsumerOptions_FluentApi_MatchesAppSettings()
    {
        // Arrange - Fluent API
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", c => c
                .FromQueue("test.queue")
                .BindToExchange("test.exchange", "test.#", "topic")
                .Durable(false)
                .Exclusive()
                .AutoDelete()
                .WithMessageTtl(TimeSpan.FromHours(1))
                .WithPrefetchCount(20)
                .WithPrefetchSize(1024)
                .WithGlobalQos()
                .WithRetry(r => r
                    .WithDelaysInSeconds(5, 10, 30)
                    .NoJitter()
                    .ThenDiscard())
                .WithDeadLetter(dlx => dlx
                    .Exchange("test.dlx")
                    .RoutingKey("test.failed")
                    .Queue("test.dlq")
                    .WithExchangeType("fanout")
                    .Durable(false)));

        var fluentOptions = GetOptions(builder);

        // Arrange - AppSettings
        var json = @"{
            ""RabbitX"": {
                ""Consumers"": {
                    ""TestConsumer"": {
                        ""Queue"": ""test.queue"",
                        ""Exchange"": ""test.exchange"",
                        ""ExchangeType"": ""topic"",
                        ""RoutingKey"": ""test.#"",
                        ""Durable"": false,
                        ""Exclusive"": true,
                        ""AutoDelete"": true,
                        ""MessageTtlSeconds"": 3600,
                        ""Qos"": {
                            ""PrefetchSize"": 1024,
                            ""PrefetchCount"": 20,
                            ""Global"": true
                        },
                        ""Retry"": {
                            ""MaxRetries"": 3,
                            ""DelaysInSeconds"": [5, 10, 30],
                            ""UseJitter"": false,
                            ""OnRetryExhausted"": ""Discard""
                        },
                        ""DeadLetter"": {
                            ""Exchange"": ""test.dlx"",
                            ""RoutingKey"": ""test.failed"",
                            ""Queue"": ""test.dlq"",
                            ""ExchangeType"": ""fanout"",
                            ""Durable"": false
                        }
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var appSettingsOptions = new RabbitXOptions();
        configuration.GetSection("RabbitX").Bind(appSettingsOptions);

        // Assert
        var fluentCon = fluentOptions.Consumers["TestConsumer"];
        var appCon = appSettingsOptions.Consumers["TestConsumer"];

        // Basic properties
        appCon.Queue.Should().Be(fluentCon.Queue);
        appCon.Exchange.Should().Be(fluentCon.Exchange);
        appCon.ExchangeType.Should().Be(fluentCon.ExchangeType);
        appCon.RoutingKey.Should().Be(fluentCon.RoutingKey);
        appCon.Durable.Should().Be(fluentCon.Durable);
        appCon.Exclusive.Should().Be(fluentCon.Exclusive);
        appCon.AutoDelete.Should().Be(fluentCon.AutoDelete);
        appCon.MessageTtlSeconds.Should().Be(fluentCon.MessageTtlSeconds);

        // QoS
        appCon.Qos.PrefetchSize.Should().Be(fluentCon.Qos.PrefetchSize);
        appCon.Qos.PrefetchCount.Should().Be(fluentCon.Qos.PrefetchCount);
        appCon.Qos.Global.Should().Be(fluentCon.Qos.Global);

        // Retry
        appCon.Retry.MaxRetries.Should().Be(fluentCon.Retry.MaxRetries);
        appCon.Retry.DelaysInSeconds.Should().BeEquivalentTo(fluentCon.Retry.DelaysInSeconds);
        appCon.Retry.UseJitter.Should().Be(fluentCon.Retry.UseJitter);
        appCon.Retry.OnRetryExhausted.Should().Be(fluentCon.Retry.OnRetryExhausted);

        // DeadLetter
        appCon.DeadLetter.Should().NotBeNull();
        appCon.DeadLetter!.Exchange.Should().Be(fluentCon.DeadLetter!.Exchange);
        appCon.DeadLetter.RoutingKey.Should().Be(fluentCon.DeadLetter.RoutingKey);
        appCon.DeadLetter.Queue.Should().Be(fluentCon.DeadLetter.Queue);
        appCon.DeadLetter.ExchangeType.Should().Be(fluentCon.DeadLetter.ExchangeType);
        appCon.DeadLetter.Durable.Should().Be(fluentCon.DeadLetter.Durable);
    }

    #endregion

    #region QoS Configuration Tests

    [Fact]
    public void QosOptions_AllProperties_CanBeSetViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Consumers"": {
                    ""TestConsumer"": {
                        ""Queue"": ""test.queue"",
                        ""Qos"": {
                            ""PrefetchSize"": 2048,
                            ""PrefetchCount"": 50,
                            ""Global"": true
                        },
                        ""Retry"": {
                            ""MaxRetries"": 0,
                            ""OnRetryExhausted"": ""Discard""
                        }
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        var consumer = options.Consumers["TestConsumer"];
        consumer.Qos.PrefetchSize.Should().Be(2048);
        consumer.Qos.PrefetchCount.Should().Be(50);
        consumer.Qos.Global.Should().BeTrue();
    }

    [Fact]
    public void QosOptions_FluentApi_AllMethodsWork()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", c => c
                .FromQueue("test.queue")
                .WithPrefetchCount(50)
                .WithPrefetchSize(2048)
                .WithGlobalQos()
                .WithRetry(r => r.MaxRetries(0).ThenDiscard()));

        var options = GetOptions(builder);

        // Assert
        var consumer = options.Consumers["TestConsumer"];
        consumer.Qos.PrefetchCount.Should().Be(50);
        consumer.Qos.PrefetchSize.Should().Be(2048);
        consumer.Qos.Global.Should().BeTrue();
    }

    [Fact]
    public void QosOptions_WithPrefetch_IsAliasForWithPrefetchCount()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", c => c
                .FromQueue("test.queue")
                .WithPrefetch(25) // Using alias
                .WithRetry(r => r.MaxRetries(0).ThenDiscard()));

        var options = GetOptions(builder);

        // Assert
        options.Consumers["TestConsumer"].Qos.PrefetchCount.Should().Be(25);
    }

    #endregion

    #region Retry Configuration Tests

    [Fact]
    public void RetryOptions_ExponentialBackoff_ViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Consumers"": {
                    ""TestConsumer"": {
                        ""Queue"": ""test.queue"",
                        ""Retry"": {
                            ""MaxRetries"": 5,
                            ""InitialDelaySeconds"": 5,
                            ""MaxDelaySeconds"": 300,
                            ""BackoffMultiplier"": 2.0,
                            ""UseJitter"": true,
                            ""OnRetryExhausted"": ""SendToDeadLetter""
                        },
                        ""DeadLetter"": {
                            ""Exchange"": ""test.dlx"",
                            ""Queue"": ""test.dlq""
                        }
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        var retry = options.Consumers["TestConsumer"].Retry;
        retry.MaxRetries.Should().Be(5);
        retry.InitialDelaySeconds.Should().Be(5);
        retry.MaxDelaySeconds.Should().Be(300);
        retry.BackoffMultiplier.Should().Be(2.0);
        retry.UseJitter.Should().BeTrue();
        retry.OnRetryExhausted.Should().Be(RetryExhaustedAction.SendToDeadLetter);
    }

    [Fact]
    public void RetryOptions_SpecificDelays_ViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Consumers"": {
                    ""TestConsumer"": {
                        ""Queue"": ""test.queue"",
                        ""Retry"": {
                            ""DelaysInSeconds"": [10, 30, 60, 120, 300],
                            ""OnRetryExhausted"": ""Requeue""
                        }
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        var retry = options.Consumers["TestConsumer"].Retry;
        retry.DelaysInSeconds.Should().BeEquivalentTo(new[] { 10, 30, 60, 120, 300 });
        retry.OnRetryExhausted.Should().Be(RetryExhaustedAction.Requeue);
    }

    #endregion

    #region DeadLetter Configuration Tests

    [Fact]
    public void DeadLetterOptions_AllProperties_CanBeSetViaAppSettings()
    {
        // Arrange
        var json = @"{
            ""RabbitX"": {
                ""Consumers"": {
                    ""TestConsumer"": {
                        ""Queue"": ""test.queue"",
                        ""Retry"": {
                            ""MaxRetries"": 3,
                            ""OnRetryExhausted"": ""SendToDeadLetter""
                        },
                        ""DeadLetter"": {
                            ""Exchange"": ""custom.dlx"",
                            ""RoutingKey"": ""custom.failed"",
                            ""Queue"": ""custom.dlq"",
                            ""ExchangeType"": ""topic"",
                            ""Durable"": false
                        }
                    }
                }
            }
        }";

        var configuration = BuildConfiguration(json);
        var options = new RabbitXOptions();

        // Act
        configuration.GetSection("RabbitX").Bind(options);

        // Assert
        var dlx = options.Consumers["TestConsumer"].DeadLetter;
        dlx.Should().NotBeNull();
        dlx!.Exchange.Should().Be("custom.dlx");
        dlx.RoutingKey.Should().Be("custom.failed");
        dlx.Queue.Should().Be("custom.dlq");
        dlx.ExchangeType.Should().Be("topic");
        dlx.Durable.Should().BeFalse();
    }

    [Fact]
    public void DeadLetterOptions_FluentApi_SimpleOverload()
    {
        // Arrange & Act
        var builder = new RabbitXOptionsBuilder()
            .UseConnection("localhost")
            .AddConsumer("TestConsumer", c => c
                .FromQueue("test.queue")
                .WithDeadLetter("custom.dlx", "custom.failed", "custom.dlq")
                .WithRetry(r => r.ThenSendToDeadLetter()));

        var options = GetOptions(builder);

        // Assert
        var dlx = options.Consumers["TestConsumer"].DeadLetter;
        dlx.Should().NotBeNull();
        dlx!.Exchange.Should().Be("custom.dlx");
        dlx.RoutingKey.Should().Be("custom.failed");
        dlx.Queue.Should().Be("custom.dlq");
    }

    #endregion

    #region Helper Methods

    private static IConfiguration BuildConfiguration(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
    }

    private static RabbitXOptions GetOptions(RabbitXOptionsBuilder builder)
    {
        var method = typeof(RabbitXOptionsBuilder).GetMethod(
            "Build",
            BindingFlags.NonPublic | BindingFlags.Instance);

        return (RabbitXOptions)method!.Invoke(builder, null)!;
    }

    #endregion
}
