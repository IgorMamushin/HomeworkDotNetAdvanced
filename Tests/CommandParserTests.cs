using System.Text;
using HomeworkAdv;

namespace CommandParserTests;

public class CommandParserTests
{
    [Fact]
    public void SuccessParseCommandWithFullArgs()
    {
        var rawString = "SET user:1 SomeData"u8.ToArray();

        var result = CommandParser.Parse(rawString);

        Assert.Equal("SET", Encoding.UTF8.GetString(result.Command));
        Assert.Equal("user:1", Encoding.UTF8.GetString(result.Key));
        Assert.Equal("SomeData", Encoding.UTF8.GetString(result.Value));
    }

    [Fact]
    public void SuccessParseCommandWithTwoArgs()
    {
        var rawString = "GET user:1"u8.ToArray();

        var result = CommandParser.Parse(rawString);

        Assert.Equal("GET", Encoding.UTF8.GetString(result.Command));
        Assert.Equal("user:1", Encoding.UTF8.GetString(result.Key));
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Value));
    }

    [Theory]
    [InlineData( "GET   user:1  Data")]
    [InlineData( " GET user:1 Data")]
    [InlineData( "GET user:1 Data ")]
    [InlineData( " GET user:1 Data ")]
    [InlineData( " GET  user:1 Data ")]
    [InlineData( " GET  user:1    Data ")]
    public void ParseCommandWithExtraWhitespaces(string input)
    {
        var result = CommandParser.Parse(Encoding.UTF8.GetBytes(input));

        Assert.Equal("GET", Encoding.UTF8.GetString(result.Command));
        Assert.Equal("user:1", Encoding.UTF8.GetString(result.Key));
        Assert.Equal("Data", Encoding.UTF8.GetString(result.Value));
    }

    [Fact]
    public void ParseCommandWithoutKey()
    {
        var rawString = "SET"u8.ToArray();

        var result = CommandParser.Parse(rawString);

        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Command));
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Key));
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Value));
    }

    [Fact]
    public void ShortCommandShouldParseSuccessfully()
    {
        var rawString = " S  U  D  "u8.ToArray();

        var result = CommandParser.Parse(rawString);

        Assert.Equal("S", Encoding.UTF8.GetString(result.Command));
        Assert.Equal("U", Encoding.UTF8.GetString(result.Key));
        Assert.Equal("D", Encoding.UTF8.GetString(result.Value));
    }

    [Fact]
    public void EmptySpanShouldBeParsedCorrectly()
    {
        var rawString = "     "u8.ToArray();

        var result = CommandParser.Parse(rawString);

        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Command));
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Key));
        Assert.Equal(string.Empty, Encoding.UTF8.GetString(result.Value));
    }
}