namespace PrintPilotProxy.Core.Interfaces;

/// <summary>
/// Provides a persistent, installation-unique instance ID that does not change across application restarts.
/// </summary>
public interface IProxyInstanceProvider
{
    /// <summary>
    /// Gets the unique persistent instance ID.
    /// </summary>
    string GetInstanceId();
}
