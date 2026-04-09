using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Models;

namespace LoadTests;

public class TcpClient : IDisposable
{
    private static byte[] s_setCommand = Encoding.UTF8.GetBytes("SET");
    private static byte[] s_getCommand = Encoding.UTF8.GetBytes("GET");
    private static byte[] s_eolCommand = Encoding.UTF8.GetBytes("\n");
    private static byte[] s_spaceCommand = Encoding.UTF8.GetBytes(" ");

    private readonly int _port;
    private readonly Socket _clientSocket;

    public TcpClient(int port)
    {
        _port = port;
        _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public ValueTask Connect(CancellationToken cancellationToken)
    {
        // localhost only by design
        return _clientSocket.ConnectAsync(IPAddress.Loopback, _port, cancellationToken);
    }

    public async Task SetValue(string key, UserProfile userProfile, CancellationToken cancellationToken)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);

        var value = JsonSerializer.SerializeToUtf8Bytes(userProfile, ModelJsonContext.Default.Options);

        var totalLength = s_setCommand.Length
                          + s_spaceCommand.Length
                          + keyBytes.Length
                          + s_spaceCommand.Length
                          + value.Length
                          + s_eolCommand.Length;

        var pool = ArrayPool<byte>.Shared.Rent(totalLength);
        try
        {
            var index = 0;
            s_setCommand.CopyTo(pool.AsMemory(index));
            index += s_setCommand.Length;
            s_spaceCommand.CopyTo(pool.AsMemory(index));
            index += s_spaceCommand.Length;
            keyBytes.CopyTo(pool.AsMemory(index));
            index += keyBytes.Length;
            s_spaceCommand.CopyTo(pool.AsMemory(index));
            index += s_spaceCommand.Length;
            value.CopyTo(pool.AsMemory(index));
            index += value.Length;
            s_eolCommand.CopyTo(pool.AsMemory(index));

            _ = await _clientSocket.SendAsync(pool.AsMemory(0, totalLength), SocketFlags.None, cancellationToken);

            var responsePool = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                var receivedBytes = await _clientSocket.ReceiveAsync(responsePool, SocketFlags.None, cancellationToken);

                if (receivedBytes == 0)
                {
                    // Console.WriteLine("Server closed the connection");
                    return;
                }

                var response = Encoding.UTF8.GetString(responsePool, 0, receivedBytes);
                // Console.WriteLine(response);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(responsePool);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pool);
        }
    }

    public async Task<UserProfile?> GetValue(string key, CancellationToken cancellationToken)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);

        var totalLength = s_getCommand.Length
                          + s_spaceCommand.Length
                          + keyBytes.Length
                          + s_eolCommand.Length;

        var pool = ArrayPool<byte>.Shared.Rent(totalLength);
        try
        {
            var index = 0;
            s_getCommand.CopyTo(pool.AsMemory(index));
            index += s_getCommand.Length;
            s_spaceCommand.CopyTo(pool.AsMemory(index));
            index += s_spaceCommand.Length;
            keyBytes.CopyTo(pool.AsMemory(index));
            index += keyBytes.Length;
            s_eolCommand.CopyTo(pool.AsMemory(index));

            _ = await _clientSocket.SendAsync(pool.AsMemory(0, totalLength), SocketFlags.None, cancellationToken);

            var responsePool = new byte[4098];
            var receivedBytes = await _clientSocket.ReceiveAsync(responsePool, SocketFlags.None, cancellationToken);

            if (receivedBytes == 0)
            {
                // Console.WriteLine("Server closed the connection");
                return null;
            }

            return JsonSerializer.Deserialize<UserProfile>(responsePool.AsSpan(0, receivedBytes), ModelJsonContext.Default.Options);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pool);
        }
    }

    public void Dispose() => _clientSocket.Dispose();
}