using Models;
using Models.Observability;

namespace HomeworkAdv;

public class SimpleStore : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<string, byte[]> _data = [];
    private long _setCount;
    private long _getCount;
    private long _deleteCount;

    public void Set(string key, UserProfile value)
    {
        using var activity = ServiceTrace.ActivitySource.StartActivity("SetData");
        activity?.SetTag("key", key);

        var data = value.Serialize();

        _lock.EnterWriteLock();
        try
        {
            if (!_data.TryAdd(key, data))
            {
                _data[key] = data;
            }

            // it's OK. because we use WriteLock
            _setCount++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public UserProfile? Get(string key)
    {
        using var activity = ServiceTrace.ActivitySource.StartActivity("GetData");
        activity?.SetTag("key", key);

        _lock.EnterReadLock();

        try
        {
            var data = _data.GetValueOrDefault(key);
            _ = Interlocked.Increment(ref _getCount);

            if (data == null)
            {
                return null;
            }

            return UserProfile.Deserialize(data);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Delete(string key)
    {
        using var activity = ServiceTrace.ActivitySource.StartActivity("DeleteData");
        activity?.SetTag("key", key);

        _lock.EnterWriteLock();
        try
        {
            var removed = _data.Remove(key);

            if (removed)
            {
                _ = Interlocked.Increment(ref _deleteCount);
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