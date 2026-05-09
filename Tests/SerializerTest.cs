using Models;

namespace CommandParserTests;

public class SerializerTest
{
    [Fact]
    public void UserSerializerTest()
    {
        var userProfile = new UserProfile
        {
            Id = 1,
            Username = "Hello",
            CreatedAt = DateTime.UtcNow
        };

        var serialized = userProfile.Serialize();

        var deserializedUserProfile = UserProfile.Deserialize(serialized);

        Assert.Equal(userProfile.Id, deserializedUserProfile.Id);
        Assert.Equal(userProfile.Username, deserializedUserProfile.Username);
        Assert.Equal(userProfile.CreatedAt, deserializedUserProfile.CreatedAt);
    }
}