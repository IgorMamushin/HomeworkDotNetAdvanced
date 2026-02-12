namespace HomeworkAdv;

public class SimpleStore
{
    private readonly Dictionary<string, byte[]> _data = new();

    public void Set(string key, byte[] value)
    {
        if (!_data.TryAdd(key, value))
        {
            _data[key] = value;
        }
    }

    public byte[]? Get(string key)
    {
        return _data.GetValueOrDefault(key);
    }

    public void Delete(string key)
    {
        _ = _data.Remove(key);
    }
}
