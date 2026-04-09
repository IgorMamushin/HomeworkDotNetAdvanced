using System.Text;
using NBomber.CSharp;

namespace LoadTests;

public class Program
{
    public static async Task Main(string[] args)
    {
        var scenario = Scenario.Create("set_get_data", async ctx =>
        {
            var key = RandomString(15);
            var value = RandomString(200);
            var valueBytes = Encoding.UTF8.GetBytes(value);

            using var tcpClient = new TcpClient(8080);

            var connect = await Step.Run("connect", ctx, async () =>
            {
                await tcpClient.Connect(ctx.ScenarioCancellationToken);
                return Response.Ok();
            });

            var set = await Step.Run("set", ctx, async () =>
            {
                await tcpClient.SetValue(key, valueBytes, ctx.ScenarioCancellationToken);
                return Response.Ok();
            });

            var get = await Step.Run("get", ctx, async () =>
            {
                var responseValue = await tcpClient.GetValue(key, ctx.ScenarioCancellationToken);
                var str = Encoding.UTF8.GetString(responseValue.Span);
                if (string.Equals(str, value))
                {
                    return Response.Ok();
                }

                return Response.Fail();
            });

            return Response.Ok();
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.RampingInject(
                rate: 200,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(30)),
            Simulation.Inject(
                rate: 200,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(30))
        );;

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }

    private static string RandomString(int length)
    {
        const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(Chars, length)
                                    .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
    }
}