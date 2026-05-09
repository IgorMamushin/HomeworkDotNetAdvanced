using System.Buffers.Binary;
using System.Text;
using HomeworkAdv;

namespace CommandParserTests;

public class CommandParserTests
{
    private static byte[] BuildFrame(string command, string key, byte[] value)
    {
        var cmdBytes = Encoding.UTF8.GetBytes(command);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var total = 4 + cmdBytes.Length + 4 + keyBytes.Length + 4 + value.Length;
        var buf = new byte[total];
        var i = 0;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i), cmdBytes.Length); i += 4;
        cmdBytes.CopyTo(buf.AsSpan(i)); i += cmdBytes.Length;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i), keyBytes.Length); i += 4;
        keyBytes.CopyTo(buf.AsSpan(i)); i += keyBytes.Length;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i), value.Length); i += 4;
        value.CopyTo(buf.AsSpan(i));
        return buf;
    }

    private static byte[] BuildFrame(string command, string key, string value)
        => BuildFrame(command, key, Encoding.UTF8.GetBytes(value));

    [Theory]
    [InlineData("SET", "user:1", "SomeData")]
    [InlineData("GET", "user:1", "")]
    [InlineData("SET", "user:99", "")]   // SET без value = удаление
    [InlineData("SET", "key", "value with spaces inside")]
    [InlineData("GET", "some-long-key:123", "")]
    [InlineData("SET", "k", "v")]
    public void ParseCommandRoundTrip(string command, string key, string value)
    {
        var frame = BuildFrame(command, key, value);

        var result = CommandParser.Parse(frame);

        Assert.Equal(command, Encoding.UTF8.GetString(result.Command));
        Assert.Equal(key, Encoding.UTF8.GetString(result.Key));
        Assert.Equal(value, Encoding.UTF8.GetString(result.Value));
    }

    [Fact]
    public void ParseCommandWithBinaryValuePreservesAllBytes()
    {
        // bytes that would corrupt the old text-based protocol
        var binaryValue = new byte[] { 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x20, 0xFF, 0x01 };
        var frame = BuildFrame("SET", "key", binaryValue);

        var result = CommandParser.Parse(frame);

        Assert.Equal("SET", Encoding.UTF8.GetString(result.Command));
        Assert.Equal("key", Encoding.UTF8.GetString(result.Key));
        Assert.Equal(binaryValue, result.Value.ToArray());
    }

    [Fact]
    public void EmptySpanReturnsAllEmpty()
    {
        var result = CommandParser.Parse([]);

        Assert.Empty(result.Command.ToArray());
        Assert.Empty(result.Key.ToArray());
        Assert.Empty(result.Value.ToArray());
    }

    [Fact]
    public void BufferTooShortForCmdLenReturnsAllEmpty()
    {
        var result = CommandParser.Parse(new byte[] { 0x03, 0x00 });

        Assert.Empty(result.Command.ToArray());
        Assert.Empty(result.Key.ToArray());
        Assert.Empty(result.Value.ToArray());
    }

    [Fact]
    public void BufferTruncatedAfterCommandReturnsCommandOnly()
    {
        var cmdBytes = Encoding.UTF8.GetBytes("GET");
        var buf = new byte[4 + cmdBytes.Length]; // нет key length после команды
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(0), cmdBytes.Length);
        cmdBytes.CopyTo(buf.AsSpan(4));

        var result = CommandParser.Parse(buf);

        Assert.Equal("GET", Encoding.UTF8.GetString(result.Command));
        Assert.Empty(result.Key.ToArray());
        Assert.Empty(result.Value.ToArray());
    }

    [Fact]
    public void BufferTruncatedAfterKeyReturnsCommandAndKey()
    {
        var cmdBytes = Encoding.UTF8.GetBytes("SET");
        var keyBytes = Encoding.UTF8.GetBytes("user:99");
        var buf = new byte[4 + cmdBytes.Length + 4 + keyBytes.Length]; // нет val length после ключа
        var i = 0;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i), cmdBytes.Length); i += 4;
        cmdBytes.CopyTo(buf.AsSpan(i)); i += cmdBytes.Length;
        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(i), keyBytes.Length); i += 4;
        keyBytes.CopyTo(buf.AsSpan(i));

        var result = CommandParser.Parse(buf);

        Assert.Equal("SET", Encoding.UTF8.GetString(result.Command));
        Assert.Equal("user:99", Encoding.UTF8.GetString(result.Key));
        Assert.Empty(result.Value.ToArray());
    }
}
