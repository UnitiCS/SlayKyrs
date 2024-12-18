using SLAU.Common.Logging;
using SLAU.Common.Models.Commands;
using SLAU.Common.Enums;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using SLAU.Common.Models;

namespace SLAU.Common.Network;
public class TcpConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ILogger _logger;
    private readonly AdaptiveBuffer _buffer;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _isDisposed;

    public bool IsConnected => _client?.Connected ?? false;
    public string RemoteEndPoint => _client?.Client?.RemoteEndPoint?.ToString() ?? "Not connected";

    public TcpConnection(TcpClient client, ILogger logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stream = client.GetStream();
        _buffer = new AdaptiveBuffer();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ObjectJsonConverter()
            }
        };
    }

    public static async Task<TcpConnection> ConnectAsync(string host, int port, ILogger logger)
    {
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(host, port);
            return new TcpConnection(client, logger);
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to connect to {host}:{port}. Error: {ex.Message}");
            throw;
        }
    }

    public async Task SendAsync<T>(T data)
    {
        try
        {
            _logger.LogDebug($"Sending data of type: {typeof(T).Name}");
            var jsonString = JsonSerializer.Serialize(data, _jsonOptions);
            _logger.LogDebug($"Serialized JSON: {jsonString}");
            var dataBytes = System.Text.Encoding.UTF8.GetBytes(jsonString);
            var lengthBytes = BitConverter.GetBytes(dataBytes.Length);

            await _stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await _stream.WriteAsync(dataBytes, 0, dataBytes.Length);
            await _stream.FlushAsync();
            _logger.LogDebug($"Data sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error sending data: {ex.Message}");
            throw;
        }
    }

    public async Task<T> ReceiveAsync<T>()
    {
        try
        {
            _logger.LogDebug($"Receiving data of type: {typeof(T).Name}");
            byte[] lengthBytes = new byte[4];
            await ReadExactAsync(lengthBytes, 0, 4);
            int dataLength = BitConverter.ToInt32(lengthBytes, 0);

            _buffer.EnsureCapacity(dataLength);
            await ReadExactAsync(_buffer.Buffer, 0, dataLength);

            var jsonString = System.Text.Encoding.UTF8.GetString(_buffer.Buffer, 0, dataLength);
            _logger.LogDebug($"Received JSON: {jsonString}");
            var result = JsonSerializer.Deserialize<T>(jsonString, _jsonOptions);
            _logger.LogDebug($"Data received and deserialized successfully as {result?.GetType().Name ?? "null"}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error receiving data: {ex.Message}");
            throw;
        }
    }

    private async Task ReadExactAsync(byte[] buffer, int offset, int count)
    {
        int totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            int bytesRead = await _stream.ReadAsync(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0)
                throw new EndOfStreamException("Connection closed by remote host");
            totalBytesRead += bytesRead;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            _stream?.Dispose();
            _client?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error disposing connection: {ex.Message}");
        }

        _isDisposed = true;
    }
}

public class ObjectJsonConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using var jsonDoc = JsonDocument.ParseValue(ref reader);
                var jsonObject = jsonDoc.RootElement;

                if (jsonObject.TryGetProperty("Type", out var typeProperty))
                {
                    CommandType commandType;
                    if (typeProperty.ValueKind == JsonValueKind.Number)
                    {
                        commandType = (CommandType)typeProperty.GetInt32();
                    }
                    else if (typeProperty.ValueKind == JsonValueKind.String)
                    {
                        if (Enum.TryParse<CommandType>(typeProperty.GetString(), true, out var parsedType))
                        {
                            commandType = parsedType;
                        }
                        else
                        {
                            throw new JsonException($"Invalid command type value: {typeProperty.GetString()}");
                        }
                    }
                    else
                    {
                        throw new JsonException($"Unexpected Type property kind: {typeProperty.ValueKind}");
                    }

                    Type targetType = GetCommandType(commandType);
                    if (targetType != null)
                    {
                        return JsonSerializer.Deserialize(jsonObject.GetRawText(), targetType, options);
                    }
                }

                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    jsonObject.GetRawText(), options);
            }

            return JsonSerializer.Deserialize<object>(ref reader, options);
        }
        catch (Exception ex)
        {
            throw new JsonException($"Error deserializing object: {ex.Message}");
        }
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    private Type GetCommandType(CommandType commandType)
    {
        return commandType switch
        {
            CommandType.InitNodes => typeof(InitNodesCommand),
            CommandType.ColumnInit => typeof(ColumnInitCommand),
            CommandType.Column => typeof(ColumnCommand),
            CommandType.Swap => typeof(SwapCommand),
            CommandType.Elimination => typeof(EliminationCommand),
            CommandType.Sync => typeof(SyncCommand),
            CommandType.Element => typeof(ElementCommand),
            CommandType.Complete => typeof(CommandComplete),
            CommandType.Matrix => typeof(Matrix),  // Добавьте это
            _ => null
        };
    }
}