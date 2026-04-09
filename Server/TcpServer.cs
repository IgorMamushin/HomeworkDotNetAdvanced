using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HomeworkAdv;

namespace Server;

public class TcpServer
{
    private readonly byte _eol;
    private readonly byte[] _unknownCommandMessage;
    private readonly byte[] _nilMessage;
    private readonly byte[] _okMessage;
    private readonly SimpleStore _store;
    private readonly SemaphoreSlim _queueLock = new(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);

    public TcpServer(SimpleStore store)
    {
        _eol = (byte)'\n';
        _unknownCommandMessage = Encoding.UTF8.GetBytes("-ERR Unknown command\r\n");
        _nilMessage = Encoding.UTF8.GetBytes("(nil)\r\n");
        _okMessage = Encoding.UTF8.GetBytes("OK\r\n");
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

                while (TryReadLine(ref buffer, out var position))
                {
                    var line = buffer.Slice(0, position.Value);
                    buffer = buffer.Slice(buffer.GetPosition(1, position.Value));

                    var length = checked((int)line.Length);
                    var rented = ArrayPool<byte>.Shared.Rent(length);
                    line.CopyTo(rented);

                    try
                    {
                        await HandleMessage(rented, stream, cancellationToken);
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
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            var result = CommandParser.Parse(buffer.Span);
            var command = Encoding.UTF8.GetString(result.Command);
            var key = Encoding.UTF8.GetString(result.Key);

            // Console.WriteLine($"Command: {command}, Key: {key}, Value: {Encoding.UTF8.GetString(result.Value)}");

            switch (command.ToLower())
            {
            case "get":
                var data = _store.Get(key) ?? _nilMessage;
                await stream.WriteAsync(data, cancellationToken);
                break;
            case "set":
                _store.Set(key, result.Value.ToArray());
                await stream.WriteAsync(_okMessage, cancellationToken);
                break;
            case "delete":
                _store.Delete(key);
                await stream.WriteAsync(_okMessage, cancellationToken);
                break;
            default:
                await stream.WriteAsync(_unknownCommandMessage, cancellationToken);
                break;
            }
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private bool TryReadLine(
        ref ReadOnlySequence<byte> buffer,
        [NotNullWhen(true)]out SequencePosition? position)
    {
        // Look for a EOL in the buffer.
        position = buffer.PositionOf(_eol);

        if (position == null)
        {
            return false;
        }

        return true;
    }
}