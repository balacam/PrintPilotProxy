namespace PrintPilotProxy.Proxy;

/// <summary>Decides whether the proxy has more client connections open than it is allowed to serve.</summary>
public static class ConnectionLimit
{
    /// <summary>True when <paramref name="currentConnections"/> (including the one being decided) exceeds <paramref name="maxConnections"/>.</summary>
    public static bool IsExceeded(int currentConnections, int maxConnections)
        => maxConnections > 0 && currentConnections > maxConnections;
}
