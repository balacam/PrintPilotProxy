using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;

namespace PrintPilotProxy.App.Services;

/// <summary>
/// Typed wrapper around IIpcClient, providing strongly-typed request/response methods
/// for all IPC message types used by the WPF application.
/// </summary>
public sealed class IpcClientService
{
    private readonly IIpcClient _client;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public IpcClientService(IIpcClient client)
    {
        _client = client;
    }

    // ─── Connection ──────────────────────────────────────────────────────────

    public bool IsConnected => _client.IsConnected;

    public Task<bool> ConnectAsync(CancellationToken ct = default)
        => _client.ConnectAsync(ct);

    // ─── Status ──────────────────────────────────────────────────────────────

    public async Task<ProxyStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcMessage { Type = IpcMessageTypes.GetStatus }, ct);
        return response?.Type == IpcMessageTypes.StatusResponse
            ? Deserialize<ProxyStatus>(response.Payload)
            : null;
    }

    // ─── Proxy Control ───────────────────────────────────────────────────────

    public async Task<(bool Success, string Message)> StartProxyAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcMessage { Type = IpcMessageTypes.StartProxy }, ct);
        return ParseResult(response);
    }

    public async Task<(bool Success, string Message)> StopProxyAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcMessage { Type = IpcMessageTypes.StopProxy }, ct);
        return ParseResult(response);
    }

    public async Task<(bool Success, string Message)> RestartProxyAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcMessage { Type = IpcMessageTypes.RestartProxy }, ct);
        return ParseResult(response);
    }

    // ─── Configuration ───────────────────────────────────────────────────────

    public async Task<ProxyConfiguration?> GetConfigurationAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcMessage { Type = IpcMessageTypes.GetConfiguration }, ct);
        return response?.Type == IpcMessageTypes.ConfigurationResponse
            ? Deserialize<ProxyConfiguration>(response.Payload)
            : null;
    }

    public async Task<(bool Success, string Message)> UpdateConfigurationAsync(
        ProxyConfiguration config, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(config, _jsonOptions);
        var response = await SendAsync(
            new IpcMessage { Type = IpcMessageTypes.UpdateConfiguration, Payload = payload }, ct);
        return ParseResult(response);
    }

    // ─── Recent Requests ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProxyRequestEntry>> GetRecentRequestsAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(new IpcMessage { Type = IpcMessageTypes.GetRecentRequests }, ct);
        if (response?.Type == IpcMessageTypes.RecentRequestsResponse && response.Payload != null)
        {
            return Deserialize<List<ProxyRequestEntry>>(response.Payload)
                   ?? new List<ProxyRequestEntry>();
        }
        return Array.Empty<ProxyRequestEntry>();
    }

    // ─── Network Interfaces ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<DiscoveredNetworkInterface>> GetNetworkInterfacesAsync(
        CancellationToken ct = default)
    {
        var response = await SendAsync(
            new IpcMessage { Type = IpcMessageTypes.GetNetworkInterfaces }, ct);
        if (response?.Type == IpcMessageTypes.NetworkInterfacesResponse && response.Payload != null)
        {
            return Deserialize<List<DiscoveredNetworkInterface>>(response.Payload)
                   ?? new List<DiscoveredNetworkInterface>();
        }
        return Array.Empty<DiscoveredNetworkInterface>();
    }

    // ─── Firewall ────────────────────────────────────────────────────────────

    public async Task<FirewallStatus?> GetFirewallStatusAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(
            new IpcMessage { Type = IpcMessageTypes.GetFirewallStatus }, ct);
        return response?.Type == IpcMessageTypes.FirewallStatusResponse
            ? Deserialize<FirewallStatus>(response.Payload)
            : null;
    }

    public async Task<(bool Success, string Message)> ApplyFirewallRuleAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(
            new IpcMessage { Type = IpcMessageTypes.ApplyFirewallRule }, ct);
        return ParseResult(response);
    }

    public async Task<(bool Success, string Message)> RemoveFirewallRuleAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(
            new IpcMessage { Type = IpcMessageTypes.RemoveFirewallRule }, ct);
        return ParseResult(response);
    }

    // ─── Security ─────────────────────────────────────────────────────────

    public async Task<SecurityAudit?> GetSecurityAuditAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(
            new IpcMessage { Type = IpcMessageTypes.GetSecurityAudit }, ct);
        return response?.Type == IpcMessageTypes.SecurityAuditResponse
            ? Deserialize<SecurityAudit>(response.Payload)
            : null;
    }

    // ─── Internal helpers ────────────────────────────────────────────────────

    private async Task<IpcMessage?> SendAsync(IpcMessage message, CancellationToken ct = default)
    {
        try
        {
            return await _client.SendAsync(message, ct);
        }
        catch
        {
            return null;
        }
    }

    private T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, _jsonOptions); }
        catch { return default; }
    }

    private static (bool Success, string Message) ParseResult(IpcMessage? response)
    {
        if (response == null)
            return (false, "No response from service. Is the service running?");
        return (response.Type == IpcMessageTypes.Success,
                response.Payload ?? response.Type);
    }
}
