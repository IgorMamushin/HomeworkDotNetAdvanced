using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HomeworkAdv;
using Microsoft.Extensions.Logging;
using Models;
using Models.Observability;

namespace Server;

public class TcpServer
{
    private readonly SimpleStore _store;
    private readonly ILogger<TcpServer> _logger;
    private static readonly byte[] s_unknownCommandMessage = Encoding.UTF8.GetBytes("-ERR Unknown command\r\n");
    private static readonly byte[] s_nilMessage = Encoding.UTF8.GetBytes("(nil)\r\n");
    private static readonly byte[] s_okMessage = Encoding.UTF8.GetBytes("OK\r\n");
    private static readonly SemaphoreSlim s_queueLock = new(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

    public TcpServer(SimpleStore store, ILogger<TcpServer> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var endPoint = new IPEndPoint(IPAddress.Any, 8080);
        socket.Bind(endPoint);
        socket.Listen(100);

        _logger.LogInformation("TCP server started on {Address}:{Port}", endPoint.Address, endPoint.Port);

        while (!cancellationToken.IsCancellationRequested)
        {
            var clientSocket = await socket.AcceptAsync(cancellationToken);
            _logger.LogInformation(
                "Accepted client connection from {RemoteEndPoint}",
                clientSocket.RemoteEndPoint);
            _ = ProcessClientAsync(clientSocket, cancellationToken);
        }

        socket.Shutdown(SocketShutdown.Both);
        _logger.LogInformation("Server stopped");
    }

    private async Task ProcessClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
        using var activity = ServiceTrace.ActivitySource.StartActivity("ClientRequest", ActivityKind.Client);

        ServiceMetrics.ActiveConnections.Add(1);

        await using var stream = new NetworkStream(clientSocket);
        var reader = PipeReader.Create(stream);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.IsEmpty)
                {
                    return;
                }

                while (TryReadMessage(ref buffer, out var message))
                {
                    var length = checked((int)message.Length);
                    var rented = ArrayPool<byte>.Shared.Rent(length);
                    message.CopyTo(rented);

                    try
                    {
                        await HandleMessage(rented.AsMemory(0, length), stream, cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            ServiceMetrics.ActiveConnections.Add(-1);

            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();
        }
    }

    private async Task HandleMessage(
        ReadOnlyMemory<byte> buffer,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        await s_queueLock.WaitAsync(cancellationToken);

        var commandStartedAt = Stopwatch.GetTimestamp();

        using var activity = ServiceTrace.ActivitySource.StartActivity("HandleMessage", ActivityKind.Client);
        activity?.SetTag("message.size", buffer.Length);

        var command = "unknown";
        var status = "ok";

        try
        {
            var result = CommandParser.Parse(buffer.Span);
            command = Encoding.UTF8.GetString(result.Command);
            var key = Encoding.UTF8.GetString(result.Key);

            activity?.SetTag("command", command);

            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["command"] = command.ToLowerInvariant(),
                ["key"] = key
            });
            switch (command.ToLowerInvariant())
            {
            case "get":
                var getUserProfile = _store.Get(key);
                if (getUserProfile == null)
                {
                    _logger.LogInformation(
                        "GET command completed with cache miss for key {Key}",
                        key);

                    await WriteWithLengthPrefixAsync(stream, s_nilMessage, cancellationToken);
                    break;
                }

                var dataSize = getUserProfile.GetByteCount();
                var responseBuffer = ArrayPool<byte>.Shared.Rent(4 + dataSize);
                try
                {
                    BinaryPrimitives.WriteInt32LittleEndian(responseBuffer, dataSize);
                    getUserProfile.SerializeTo(responseBuffer.AsSpan(4, dataSize));
                    await stream.WriteAsync(responseBuffer.AsMemory(0, 4 + dataSize), cancellationToken);

                    _logger.LogInformation(
                        "GET command completed with cache hit for key {Key}, response size {ResponseSizeBytes} bytes",
                        key,
                        dataSize);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(responseBuffer);
                }

                break;
            case "set":
                var length = result.Value.Length;
                var userProfile = UserProfile.Deserialize(result.Value) ??
                                  throw new ArgumentException("Cannot deserialize user profile");

                _store.Set(key, userProfile);
                await WriteWithLengthPrefixAsync(stream, s_okMessage, cancellationToken);

                _logger.LogInformation(
                    "SET command completed for key {Key}, value size {ValueSizeBytes} bytes",
                    key,
                    length);
                break;
            case "delete":
                _store.Delete(key);
                await WriteWithLengthPrefixAsync(stream, s_okMessage, cancellationToken);

                _logger.LogInformation(
                    "DELETE command completed for key {Key}",
                    key);
                break;
            default:
                status = "unknown_command";

                _logger.LogWarning(
                    "Unknown command received: {Command}",
                    command);
                await WriteWithLengthPrefixAsync(stream, s_unknownCommandMessage, cancellationToken);
                break;
            }
        }
        catch (OperationCanceledException)
        {
            status = "cancelled";

            _logger.LogInformation(
                "Command handling cancelled for command {Command}",
                command);
        }
        catch (Exception e)
        {
            status = "error";

            ServiceMetrics.RequestErrorsTotal.Add(1,
                new KeyValuePair<string, object?>("command", command),
                new KeyValuePair<string, object?>("error_type", e.GetType().Name));

            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            activity?.SetTag("StrBuffer", Encoding.UTF8.GetString(buffer.Span));

            _logger.LogError(
                e,
                "Command handling failed for command {Command}, message size {MessageSizeBytes} bytes",
                command,
                buffer.Length);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var commandElapsedMs = Stopwatch.GetElapsedTime(commandStartedAt).TotalMilliseconds;

            ServiceMetrics.RequestDuration.Record(elapsedMs,
                new KeyValuePair<string, object?>("command", command.ToLowerInvariant()),
                new KeyValuePair<string, object?>("status", status));

            ServiceMetrics.CommandProcessingDuration.Record(commandElapsedMs,
                new KeyValuePair<string, object?>("command", command.ToLowerInvariant()),
                new KeyValuePair<string, object?>("status", status));

            ServiceMetrics.RequestsTotal.Add(1,
                new KeyValuePair<string, object?>("command", command.ToLowerInvariant()),
                new KeyValuePair<string, object?>("status", status));

            _ = s_queueLock.Release();
        }
    }

    private static bool TryReadMessage(
        ref ReadOnlySequence<byte> buffer,
        out ReadOnlySequence<byte> message)
    {
        using var activity = ServiceTrace.ActivitySource.StartActivity("TryReadMessage", ActivityKind.Client);

        if (buffer.Length < 4)
        {
            message = default;
            return false;
        }

        Span<byte> lengthBytes = stackalloc byte[4];
        buffer.Slice(0, 4).CopyTo(lengthBytes);
        var messageLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);

        if (buffer.Length < 4 + messageLength)
        {
            message = default;
            return false;
        }

        message = buffer.Slice(4, messageLength);
        buffer = buffer.Slice(4 + messageLength);
        return true;
    }

    private static async ValueTask WriteWithLengthPrefixAsync(
        Stream stream,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        using var activity = ServiceTrace.ActivitySource.StartActivity("WriteMessage", ActivityKind.Client);

        byte[] rented = ArrayPool<byte>.Shared.Rent(4);

        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(rented.AsSpan(0, 4), payload.Length);

            await stream.WriteAsync(rented.AsMemory(0, 4), cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}