using HomeworkAdv;

namespace CommandParserTests;

public class CommandParserTests
{
    [Fact]
    public void SuccessParseCommandWithFullArgs()
    {
        var rawString = "SET user:1 SomeData";

        var result = CommandParser.Parse(rawString);

        Assert.Equal("SET", result.Command.ToString());
        Assert.Equal("user:1", result.Key.ToString());
        Assert.Equal("SomeData", result.Value.ToString());
    }

    [Fact]
    public void SuccessParseCommandWithTwoArgs()
    {
        var rawString = "GET user:1";

        var result = CommandParser.Parse(rawString);

        Assert.Equal("GET", result.Command.ToString());
        Assert.Equal("user:1", result.Key.ToString());
        Assert.Equal(string.Empty, result.Value.ToString());
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
        var result = CommandParser.Parse(input);

        Assert.Equal("GET", result.Command.ToString());
        Assert.Equal("user:1", result.Key.ToString());
        Assert.Equal("Data", result.Value.ToString());
    }

    [Fact]
    public void ParseCommandWithoutKey()
    {
        var rawString = "SET";

        var result = CommandParser.Parse(rawString);

        Assert.Equal(string.Empty, result.Command.ToString());
        Assert.Equal(string.Empty, result.Key.ToString());
        Assert.Equal(string.Empty, result.Value.ToString());
    }

    [Fact]
    public void ShortCommandShouldParseSuccessfully()
    {
        var rawString = " S  U  D  ";

        var result = CommandParser.Parse(rawString);

        Assert.Equal("S", result.Command.ToString());
        Assert.Equal("U", result.Key.ToString());
        Assert.Equal("D", result.Value.ToString());
    }

    [Fact]
    public void EmptySpanShouldBeParsedCorrectly()
    {
        var rawString = "     ";

        var result = CommandParser.Parse(rawString);

        Assert.Equal(string.Empty, result.Command.ToString());
        Assert.Equal(string.Empty, result.Key.ToString());
        Assert.Equal(string.Empty, result.Value.ToString());
    }
}