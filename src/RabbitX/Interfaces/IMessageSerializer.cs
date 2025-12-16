namespace RabbitX.Interfaces;

/// <summary>
/// Serializer interface for message serialization/deserialization.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Serializes a message to bytes.
    /// </summary>
    /// <typeparam name="TMessage">The type of message.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>Serialized bytes.</returns>
    byte[] Serialize<TMessage>(TMessage message) where TMessage : class;

    /// <summary>
    /// Deserializes bytes to a message.
    /// </summary>
    /// <typeparam name="TMessage">The type of message.</typeparam>
    /// <param name="data">The bytes to deserialize.</param>
    /// <returns>Deserialized message.</returns>
    TMessage Deserialize<TMessage>(byte[] data) where TMessage : class;

    /// <summary>
    /// Deserializes bytes to a message of the specified type.
    /// Used for RPC responses where the type is determined at runtime.
    /// </summary>
    /// <param name="data">The bytes to deserialize.</param>
    /// <param name="type">The type to deserialize to.</param>
    /// <returns>Deserialized message.</returns>
    object Deserialize(ReadOnlyMemory<byte> data, Type type);

    /// <summary>
    /// Gets the content type for this serializer (e.g., "application/json").
    /// </summary>
    string ContentType { get; }
}
