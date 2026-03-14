namespace HomeworkAdv;

public class SimpleStore : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<string, byte[]> _data = new();
    private long _setCount;
    private long _getCount;
    private long _deleteCount;

    public void Set(string key, byte[] value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_data.TryAdd(key, value))
            {
                _data[key] = value;
            }

            // it's OK. because we use WriteLock
            _setCount++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public byte[]? Get(string key)
    {
        _lock.EnterReadLock();

        try
        {
            var data = _data.GetValueOrDefault(key);
            Interlocked.Increment(ref _getCount);
            return data;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Delete(string key)
    {
        _lock.EnterWriteLock();
        try
        {
            var removed = _data.Remove(key);

            if (removed)
            {
                Interlocked.Increment(ref _deleteCount);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public StoreStatistic GetStatistics()
    {
        var getCount = Interlocked.Read(ref _getCount);
        var setCount = Interlocked.Read(ref _setCount);
        var deleteCount = Interlocked.Read(ref _deleteCount);

        return new StoreStatistic(setCount, getCount, deleteCount);
    }

    public void Dispose() => _lock.Dispose();
}