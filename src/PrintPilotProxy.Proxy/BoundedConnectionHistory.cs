namespace PrintPilotProxy.Proxy;

/// <summary>A bounded diagnostic history; evicted connections may be logged again without affecting routing.</summary>
public sealed class BoundedConnectionHistory
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly HashSet<Guid> _ids = new();
    private readonly Queue<Guid> _order = new();

    public BoundedConnectionHistory(int capacity = 32_768)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Count { get { lock (_gate) return _ids.Count; } }

    public bool TryAdd(Guid id)
    {
        lock (_gate)
        {
            if (_ids.Contains(id)) return false;
            if (_ids.Count == _capacity) _ids.Remove(_order.Dequeue());
            _ids.Add(id);
            _order.Enqueue(id);
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate) { _ids.Clear(); _order.Clear(); }
    }
}
