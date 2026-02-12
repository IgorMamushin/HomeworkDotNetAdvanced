using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HomeworkAdv;

namespace Server;

public class TcpServer
{
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
        const int ChunkSize = 8 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        var allBytes = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var readBytes = await clientSocket.ReceiveAsync(
                    buffer.AsMemory(),
                    SocketFlags.None,
                    cancellationToken);

                allBytes += readBytes;

                if (readBytes == 0)
                {
                    if (allBytes > 0)
                    {
                        var result = CommandParser.Parse(buffer.AsMemory(0, allBytes).Span);
                        Console.WriteLine($"Command: {Encoding.UTF8.GetString(result.Command)}, Key: {Encoding.UTF8.GetString(result.Key)}, Value: {Encoding.UTF8.GetString(result.Value)}");
                    }
                    
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();
        }
    }
}