using System.Buffers.Binary;

namespace HomeworkAdv;

public static class CommandParser
{
    public static ParseResult<byte> Parse(ReadOnlySpan<byte> span)
    {
        if (span.Length < 4)
        {
            return new ParseResult<byte>([], [], []);
        }

        var cmdLen = BinaryPrimitives.ReadInt32LittleEndian(span);
        span = span[4..];
        if (span.Length < cmdLen)
        {
            return new ParseResult<byte>([], [], []);
        }

        var command = span[..cmdLen];
        span = span[cmdLen..];

        if (span.Length < 4)
        {
            return new ParseResult<byte>(command, [], []);
        }

        var keyLen = BinaryPrimitives.ReadInt32LittleEndian(span);
        span = span[4..];
        if (span.Length < keyLen)
        {
            return new ParseResult<byte>(command, [], []);
        }

        var key = span[..keyLen];
        span = span[keyLen..];

        if (span.Length < 4)
        {
            return new ParseResult<byte>(command, key, []);
        }

        var valLen = BinaryPrimitives.ReadInt32LittleEndian(span);
        span = span[4..];
        if (span.Length < valLen)
        {
            return new ParseResult<byte>(command, key, []);
        }

        var value = span[..valLen];

        return new ParseResult<byte>(command, key, value);
    }
}
