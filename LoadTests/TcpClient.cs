using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Models;

namespace LoadTests;

public class TcpClient : IDisposable
{
    private static readonly byte[] s_setCommand = "SET"u8.ToArray();
    private static readonly byte[] s_getCommand = "GET"u8.ToArray();

    private readonly int _port;
    private readonly Socket _clientSocket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public TcpClient(int port)
    {
        _port = port;
    }

    public ValueTask Connect(CancellationToken cancellationToken)
    {
        // localhost only by design
        return _clientSocket.ConnectAsync(IPAddress.Loopback, _port, cancellationToken);
    }

    public async Task SetValue(string key, UserProfile userProfile, CancellationToken cancellationToken)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var value = userProfile.Serialize();

        // frame: [4-byte tcp_len][4-byte cmd_len][cmd][4-byte key_len][key][4-byte val_len][val]
        var messageLength = 4 + s_setCommand.Length + 4 + keyBytes.Length + 4 + value.Length;
        var frameLength = 4 + messageLength;

        var pool = ArrayPool<byte>.Shared.Rent(frameLength);
        try
        {
            var i = 0;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), messageLength); i += 4;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), s_setCommand.Length); i += 4;
            s_setCommand.CopyTo(pool.AsMemory(i)); i += s_setCommand.Length;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), keyBytes.Length); i += 4;
            keyBytes.CopyTo(pool.AsMemory(i)); i += keyBytes.Length;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), value.Length); i += 4;
            value.CopyTo(pool.AsMemory(i));

            _ = await _clientSocket.SendAsync(pool.AsMemory(0, frameLength), SocketFlags.None, cancellationToken);
            await ReceiveResponseAsync(cancellationToken);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pool);
        }
    }

    public async Task<UserProfile?> GetValue(string key, CancellationToken cancellationToken)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);

        // frame: [4-byte tcp_len][4-byte cmd_len][cmd][4-byte key_len][key][4-byte val_len=0]
        var messageLength = 4 + s_getCommand.Length + 4 + keyBytes.Length + 4;
        var frameLength = 4 + messageLength;

        var pool = ArrayPool<byte>.Shared.Rent(frameLength);
        try
        {
            var i = 0;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), messageLength); i += 4;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), s_getCommand.Length); i += 4;
            s_getCommand.CopyTo(pool.AsMemory(i)); i += s_getCommand.Length;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), keyBytes.Length); i += 4;
            keyBytes.CopyTo(pool.AsMemory(i)); i += keyBytes.Length;
            BinaryPrimitives.WriteInt32LittleEndian(pool.AsSpan(i), 0); // empty value

            _ = await _clientSocket.SendAsync(pool.AsMemory(0, frameLength), SocketFlags.None, cancellationToken);

            var responseData = await ReceiveResponseAsync(cancellationToken);
            if (responseData == null)
            {
                return null;
            }

            return UserProfile.Deserialize(responseData);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pool);
        }
    }

    private async Task<byte[]?> ReceiveResponseAsync(CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        var received = 0;
        while (received < 4)
        {
            var n = await _clientSocket.ReceiveAsync(prefix.AsMemory(received, 4 - received), SocketFlags.None, cancellationToken);
            if (n == 0)
            {
                return null;
            }

            received += n;
        }

        var dataLength = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        var data = new byte[dataLength];
        received = 0;
        while (received < dataLength)
        {
            var n = await _clientSocket.ReceiveAsync(data.AsMemory(received, dataLength - received), SocketFlags.None, cancellationToken);
            if (n == 0)
            {
                return null;
            }

            received += n;
        }

        return data;
    }

    public void Dispose() => _clientSocket.Dispose();
}
