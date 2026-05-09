using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HomeworkAdv;
using Models;

namespace Server;

public class TcpServer
{
    private readonly SimpleStore _store;
    private static readonly byte[] s_unknownCommandMessage = Encoding.UTF8.GetBytes("-ERR Unknown command\r\n");
    private static readonly byte[] s_nilMessage = Encoding.UTF8.GetBytes("(nil)\r\n");
    private static readonly byte[] s_okMessage = Encoding.UTF8.GetBytes("OK\r\n");
    private static readonly SemaphoreSlim s_queueLock = new(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

    public TcpServer(SimpleStore store)
    {
        _store = store;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        socket.Bind(endPoint);
        socket.Listen(100);

        while (!cancellationToken.IsCancellationRequested)
        {
            var clientSocket = await socket.AcceptAsync(cancellationToken);
            _ = ProcessClientAsync(clientSocket, cancellationToken);
        }

        socket.Shutdown(SocketShutdown.Both);
    }

    private async Task ProcessClientAsync(Socket clientSocket, CancellationToken cancellationToken)
    {
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
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();
            // Console.WriteLine("Client disconnected");
        }
    }

    private async Task HandleMessage(
        ReadOnlyMemory<byte> buffer,
        Stream stream,
        CancellationToken cancellationToken)
    {
        await s_queueLock.WaitAsync(cancellationToken);

        try
        {
            var result = CommandParser.Parse(buffer.Span);
            var command = Encoding.UTF8.GetString(result.Command);
            var key = Encoding.UTF8.GetString(result.Key);

            // Console.WriteLine($"Command: {command}, Key: {key}, Value: {Encoding.UTF8.GetString(result.Value)}");

            switch (command.ToLower())
            {
            case "get":
                var getUserProfile = _store.Get(key);
                if (getUserProfile == null)
                {
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
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(responseBuffer);
                }

                break;
            case "set":
                var userProfile = UserProfile.Deserialize(result.Value) ??
                                  throw new ArgumentException("Cannot deserialize user profile");

                _store.Set(key, userProfile);
                await WriteWithLengthPrefixAsync(stream, s_okMessage, cancellationToken);
                break;
            case "delete":
                _store.Delete(key);
                await WriteWithLengthPrefixAsync(stream, s_okMessage, cancellationToken);
                break;
            default:
                await WriteWithLengthPrefixAsync(stream, s_unknownCommandMessage, cancellationToken);
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception e)
        {
            var strBuffer = Encoding.UTF8.GetString(buffer.Span);

            Console.WriteLine($"Buffer: {strBuffer}");
            Console.WriteLine(e);
        }
        finally
        {
            _ = s_queueLock.Release();
        }
    }

    private static bool TryReadMessage(
        ref ReadOnlySequence<byte> buffer,
        out ReadOnlySequence<byte> message)
    {
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