namespace HomeworkAdv;

public static class CommandParser
{
    private const byte Separator = (byte)' ';

    public static ParseResult<byte> Parse(ReadOnlySpan<byte> parentSpan)
    {
        var firstNonEmptyIndex = IndexOfFirstNonWhitespace(ref parentSpan);

        parentSpan = parentSpan.Slice(firstNonEmptyIndex);

        var index = parentSpan.IndexOf(Separator);
        if (index == -1)
        {
            return new ParseResult<byte>(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty);
        }

        var command = parentSpan.Slice(0, index);
        parentSpan = parentSpan.Slice(index + 1);
        firstNonEmptyIndex = IndexOfFirstNonWhitespace(ref parentSpan);
        parentSpan = parentSpan.Slice(firstNonEmptyIndex);

        index = parentSpan.IndexOf(Separator);
        if (index == -1)
        {
            if (parentSpan.Length > 0)
            {
                var length = LengthWithoutWhitespace(ref parentSpan);

                return new ParseResult<byte>(command, parentSpan.Slice(0, length), ReadOnlySpan<byte>.Empty);
            }

            return new ParseResult<byte>(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty);
        }

        var key = parentSpan.Slice(0, index);
        parentSpan = parentSpan.Slice(index+1);
        firstNonEmptyIndex = IndexOfFirstNonWhitespace(ref parentSpan);
        parentSpan = parentSpan.Slice(firstNonEmptyIndex);

        if (parentSpan.Length <= 0)
        {
            return new ParseResult<byte>(command, key, ReadOnlySpan<byte>.Empty);
        }

        var lengthValue = LengthWithoutWhitespace(ref parentSpan);

        return new ParseResult<byte>(command, key, parentSpan.Slice(0, lengthValue));
    }

    private static int IndexOfFirstNonWhitespace(ref readonly ReadOnlySpan<byte> span)
    {
        var i = 0;
        while (i < span.Length && span[i] == Separator)
        {
            i++;
        }

        return i;
    }

    private static int LengthWithoutWhitespace(ref readonly ReadOnlySpan<byte> span)
    {
        var i = span.Length - 1;
        while (i >= 0 && span[i] == Separator)
        {
            i--;
        }

        return i+1;
    }
}