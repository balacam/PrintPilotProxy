using System;
using System.Collections.Concurrent;
using System.Net;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Discovery;

/// <summary>
/// Sliding-window rate limiter per client IP to protect UDP discovery against floods.
/// </summary>
public sealed class DiscoveryRateLimiter : IDiscoveryRateLimiter
{
    private readonly int _maxRequestsPerWindow;
    private readonly TimeSpan _windowDuration;
    private readonly ConcurrentDictionary<IPAddress, RequestCounter> _clients = new();
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
    private readonly object _cleanupLock = new();

    public DiscoveryRateLimiter(int maxRequestsPerWindow = 10, TimeSpan? windowDuration = null)
    {
        _maxRequestsPerWindow = Math.Max(1, maxRequestsPerWindow);
        _windowDuration = windowDuration ?? TimeSpan.FromSeconds(5);
    }

    public bool ShouldAllow(IPAddress clientAddress)
    {
        var now = DateTimeOffset.UtcNow;
        CleanupExpired(now);

        var counter = _clients.GetOrAdd(clientAddress, _ => new RequestCounter(now));
        return counter.TryRecordRequest(now, _maxRequestsPerWindow, _windowDuration);
    }

    public void Reset()
    {
        _clients.Clear();
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        if (now - _lastCleanup < TimeSpan.FromSeconds(30))
        {
            return;
        }

        if (Monitor.TryEnter(_cleanupLock))
        {
            try
            {
                if (now - _lastCleanup >= TimeSpan.FromSeconds(30))
                {
                    var cutoff = now - _windowDuration;
                    foreach (var kvp in _clients)
                    {
                        if (kvp.Value.IsExpired(cutoff))
                        {
                            _clients.TryRemove(kvp.Key, out _);
                        }
                    }
                    _lastCleanup = now;
                }
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }
    }

    private sealed class RequestCounter
    {
        private readonly object _sync = new();
        private DateTimeOffset _windowStart;
        private int _requestCount;
        private DateTimeOffset _lastSeen;

        public RequestCounter(DateTimeOffset now)
        {
            _windowStart = now;
            _requestCount = 0;
            _lastSeen = now;
        }

        public bool TryRecordRequest(DateTimeOffset now, int maxRequests, TimeSpan window)
        {
            lock (_sync)
            {
                _lastSeen = now;
                if (now - _windowStart > window)
                {
                    _windowStart = now;
                    _requestCount = 1;
                    return true;
                }

                if (_requestCount < maxRequests)
                {
                    _requestCount++;
                    return true;
                }

                return false;
            }
        }

        public bool IsExpired(DateTimeOffset cutoff)
        {
            lock (_sync)
            {
                return _lastSeen < cutoff;
            }
        }
    }
}
