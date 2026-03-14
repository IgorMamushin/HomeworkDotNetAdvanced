namespace HomeworkAdv;

public readonly ref struct StoreStatistic
{
    public StoreStatistic(
        long setCount,
        long getCount,
        long deleteCount)
    {
        SetCount = setCount;
        GetCount = getCount;
        DeleteCount = deleteCount;
    }

    public long SetCount { get; }
    public long GetCount { get; }
    public long DeleteCount { get; }
}