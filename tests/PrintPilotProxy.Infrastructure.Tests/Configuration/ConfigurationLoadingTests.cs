using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Configuration;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Configuration;

/// <summary>PROXY-016: enum names from the documentation load, and a missing section does not null-crash the service.</summary>
public class ConfigurationLoadingTests : IDisposable
{
    private readonly string _configPath = Path.Combine(Path.GetTempPath(), $"load_config_{Guid.NewGuid()}.json");
    private readonly JsonConfigurationManager _manager;

    public ConfigurationLoadingTests()
    {
        var paths = new Mock<IPlatformPathProvider>();
        var backups = Path.Combine(Path.GetTempPath(), $"load_backups_{Guid.NewGuid()}");
        Directory.CreateDirectory(backups);
        paths.Setup(p => p.ConfigurationFilePath).Returns(_configPath);
        paths.Setup(p => p.BackupDirectory).Returns(backups);
        _manager = new JsonConfigurationManager(paths.Object, NullLogger<JsonConfigurationManager>.Instance);
    }

    public void Dispose()
    {
        try { File.Delete(_configPath); } catch (IOException) { }
    }

    [Fact]
    public async Task LoadAsync_AcceptsEnumNames()
    {
        await File.WriteAllTextAsync(_configPath,
            "{\"schemaVersion\":2,\"listener\":{\"mode\":\"specificAddress\",\"listenAddress\":\"127.0.0.1\",\"port\":3128}," +
            "\"clientAccess\":{\"mode\":\"allowList\"},\"upstreamProxy\":{\"mode\":\"manual\",\"host\":\"p\",\"port\":8080}}");

        var config = await _manager.LoadAsync();

        config.Listener.Mode.Should().Be(ListenerMode.SpecificAddress);
        config.ClientAccess.Mode.Should().Be(ClientAccessMode.AllowList);
        config.UpstreamProxy.Mode.Should().Be(UpstreamProxyMode.Manual);
    }

    [Fact]
    public async Task LoadAsync_StillAcceptsNumericEnums()
    {
        await File.WriteAllTextAsync(_configPath,
            "{\"schemaVersion\":2,\"listener\":{\"mode\":1,\"listenAddress\":\"127.0.0.1\",\"port\":3128}}");

        var config = await _manager.LoadAsync();

        config.Listener.Mode.Should().Be(ListenerMode.SpecificAddress);
    }

    [Fact]
    public async Task LoadAsync_NullSections_AreReplacedWithDefaults()
    {
        await File.WriteAllTextAsync(_configPath,
            "{\"schemaVersion\":2,\"listener\":null,\"security\":null,\"clientAccess\":null,\"upstreamProxy\":null}");

        var config = await _manager.LoadAsync();

        config.Listener.Should().NotBeNull();
        config.Security.Should().NotBeNull();
        config.ClientAccess.AllowedClients.Should().NotBeNull();
        config.UpstreamProxy.Should().NotBeNull();
    }
}
