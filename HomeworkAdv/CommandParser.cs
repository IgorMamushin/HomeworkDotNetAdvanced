namespace HomeworkAdv;

public static class CommandParser
{
    private const string Separator = " ";

    public static ParseResult<char> Parse(ReadOnlySpan<char> parentSpan)
    {
        parentSpan = parentSpan.Trim();
        var index = parentSpan.IndexOf(Separator);
        if (index == -1)
        {
            return new ParseResult<char>(ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty);
        }

        var command = parentSpan.Slice(0, index);
        parentSpan = parentSpan.Slice(index+1).Trim();

        index = parentSpan.IndexOf(Separator);
        if (index == -1)
        {
            if (parentSpan.Length > 0)
            {
                return new ParseResult<char>(command, parentSpan.Trim(), ReadOnlySpan<char>.Empty);
            }

            return new ParseResult<char>(ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty);
        }

        var key = parentSpan.Slice(0, index);
        parentSpan = parentSpan.Slice(index+1).Trim();

        if (parentSpan.Length <= 0)
        {
            return new ParseResult<char>(command, key, ReadOnlySpan<char>.Empty);
        }

        return new ParseResult<char>(command, key, parentSpan);
    }
}