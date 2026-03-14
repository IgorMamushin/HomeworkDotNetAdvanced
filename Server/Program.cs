using HomeworkAdv;

namespace Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var globalCts = new CancellationTokenSource();
        Console.WriteLine("Press ctrl+c to cancel command");
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            globalCts.Cancel();
            Console.WriteLine("Cancelling...");
        };

        using var store = new SimpleStore();

        var server = new TcpServer(store);
        await server.StartAsync(globalCts.Token);
    }
}