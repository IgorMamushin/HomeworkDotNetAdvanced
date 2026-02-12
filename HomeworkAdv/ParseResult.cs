namespace HomeworkAdv;

public readonly ref struct ParseResult<T>
{
    public ParseResult(
        ReadOnlySpan<T> command,
        ReadOnlySpan<T> key,
        ReadOnlySpan<T> value)
    {
        Command = command;
        Key = key;
        Value = value;
    }

    public ReadOnlySpan<T> Command { get; }
    public ReadOnlySpan<T> Key { get; }
    public ReadOnlySpan<T> Value { get; }
}