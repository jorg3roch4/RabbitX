using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitX.Interfaces;

namespace RabbitX.Serialization;

/// <summary>
/// JSON serializer using System.Text.Json.
/// </summary>
public sealed class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageSerializer"/> class with default options.
    /// </summary>
    public JsonMessageSerializer() : this(CreateDefaultOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonMessageSerializer"/> class with custom options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use.</param>
    public JsonMessageSerializer(JsonSerializerOptions options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public string ContentType => "application/json";

    /// <inheritdoc />
    public byte[] Serialize<TMessage>(TMessage message) where TMessage : class
    {
        var json = JsonSerializer.Serialize(message, _options);
        return Encoding.UTF8.GetBytes(json);
    }

    /// <inheritdoc />
    public TMessage Deserialize<TMessage>(byte[] data) where TMessage : class
    {
        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<TMessage>(json, _options)
            ?? throw new InvalidOperationException($"Failed to deserialize message to {typeof(TMessage).Name}");
    }

    /// <inheritdoc />
    public object Deserialize(ReadOnlyMemory<byte> data, Type type)
    {
        var json = Encoding.UTF8.GetString(data.Span);
        return JsonSerializer.Deserialize(json, type, _options)
            ?? throw new InvalidOperationException($"Failed to deserialize message to {type.Name}");
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }
}
