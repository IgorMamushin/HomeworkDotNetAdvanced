using Models;
using NBomber.CSharp;

namespace LoadTests;

public class Program
{
    private static int s_id = 0;

    public static async Task Main(string[] args)
    {
        // var key = RandomString(15);
        // var userProfile = GenerateUserProfile();
        // using var tcpClient = new TcpClient(8080);
        //
        // await tcpClient.Connect(CancellationToken.None);
        // await tcpClient.SetValue(key, userProfile, CancellationToken.None);


        var scenario = Scenario.Create("set_get_data", async ctx =>
        {
            var key = RandomString(15);
            var userProfile = GenerateUserProfile();

            using var tcpClient = new TcpClient(8080);

            var connect = await Step.Run("connect", ctx, async () =>
            {
                await tcpClient.Connect(ctx.ScenarioCancellationToken);
                return Response.Ok();
            });

            var set = await Step.Run("set", ctx, async () =>
            {
                await tcpClient.SetValue(key, userProfile, ctx.ScenarioCancellationToken);
                return Response.Ok();
            });

            var get = await Step.Run("get", ctx, async () =>
            {
                var responseValue = await tcpClient.GetValue(key, ctx.ScenarioCancellationToken);
                if (responseValue?.Id == userProfile.Id)
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
                rate: 10,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(30)),
            Simulation.Inject(
                rate: 10,
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

    private static string RandomString(int minLength, int maxLength)
    {
        var length = Random.Shared.Next(minLength, maxLength);
        return RandomString(length);
    }

    private static UserProfile GenerateUserProfile()
    {
        return new UserProfile
        {
            Id = Interlocked.Add(ref s_id, 1),
            CreatedAt = DateTime.UtcNow,
            Username = RandomString(10)
        };
    }
}