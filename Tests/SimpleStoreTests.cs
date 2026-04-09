using HomeworkAdv;
using Models;

namespace CommandParserTests;

public class SimpleStoreTests
{
    [Fact]
    public async Task MultiThreadTest()
    {
        using var sut = new SimpleStore();

        const string Key = "key1";

        var userProfile = new UserProfile()
        {
            Username = "Test",
            Id = 1,
            CreatedAt = DateTime.Now
        };

        const int SetCalls = 1000;
        const int GetCalls = 1000;

        var tasksSetValue = Enumerable.Range(0, SetCalls)
                                       .Select(_ => Task.Run(() => sut.Set(Key, userProfile)));

        var tasksGetValue = Enumerable.Range(0, GetCalls)
                                          .Select(_ => Task.Run(() => sut.Get(Key)));

        await Task.WhenAll(tasksSetValue.Union(tasksGetValue));
        var statistics = sut.GetStatistics();

        Assert.Equal(SetCalls, statistics.SetCount);
        Assert.Equal(GetCalls, statistics.GetCount);
        Assert.Equal(0, statistics.DeleteCount);
    }
}