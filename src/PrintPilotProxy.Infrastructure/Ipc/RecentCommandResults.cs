using System;
using System.Collections.Generic;
using System.Linq;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.Infrastructure.Ipc;

/// <summary>
/// Remembers the answer to recent state-changing IPC commands by correlation id. When a client times out and sends the
/// same command again, the earlier answer is returned instead of applying the change a second time.
/// Bounded in size and age.
/// </summary>
public sealed class RecentCommandResults
{
    private readonly object _lock = new();
    private readonly Dictionary<string, (DateTime StoredUtc, IpcMessage Response)> _entries = new(StringComparer.Ordinal);
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly Func<DateTime> _clock;

    public RecentCommandResults(int capacity = 64, TimeSpan? ttl = null, Func<DateTime>? clock = null)
    {
        _capacity = Math.Max(1, capacity);
        _ttl = ttl ?? TimeSpan.FromMinutes(2);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public bool TryGet(string? correlationId, string commandType, out IpcMessage response)
    {
        response = null!;
        if (string.IsNullOrWhiteSpace(correlationId)) return false;

        lock (_lock)
        {
            if (_entries.TryGetValue(Key(correlationId, commandType), out var entry) && _clock() - entry.StoredUtc <= _ttl)
            {
                response = entry.Response;
                return true;
            }
        }

        return false;
    }

    public void Store(string? correlationId, string commandType, IpcMessage response)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return;

        lock (_lock)
        {
            var now = _clock();
            foreach (var stale in _entries.Where(e => now - e.Value.StoredUtc > _ttl).Select(e => e.Key).ToList())
            {
                _entries.Remove(stale);
            }

            while (_entries.Count >= _capacity)
            {
                var oldest = _entries.OrderBy(e => e.Value.StoredUtc).First().Key;
                _entries.Remove(oldest);
            }

            _entries[Key(correlationId, commandType)] = (now, response);
        }
    }

    private static string Key(string correlationId, string commandType) => commandType + "\n" + correlationId;
}
