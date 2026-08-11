using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Configuration;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Configuration;

public class ConfigurationManagerTests
{
    private readonly Mock<IPlatformPathProvider> _mockPathProvider;
    private readonly JsonConfigurationManager _manager;
    private readonly string _testConfigPath;

    public ConfigurationManagerTests()
    {
        _mockPathProvider = new Mock<IPlatformPathProvider>();
        _testConfigPath = Path.Combine(Path.GetTempPath(), $"test_config_{System.Guid.NewGuid()}.json");
        var testBackupPath = Path.Combine(Path.GetTempPath(), $"test_backups_{System.Guid.NewGuid()}");
        Directory.CreateDirectory(testBackupPath);
        _mockPathProvider.Setup(p => p.ConfigurationFilePath).Returns(_testConfigPath);
        _mockPathProvider.Setup(p => p.BackupDirectory).Returns(testBackupPath);
        _mockPathProvider.Setup(p => p.EnsureDirectoriesExist()).Callback(() => { });

        _manager = new JsonConfigurationManager(_mockPathProvider.Object, NullLogger<JsonConfigurationManager>.Instance);
    }

    [Fact]
    public async Task LoadAsync_SchemaV1_MigratesEmptyClientsToAllowList()
    {
        var v1Json = @"
        {
            ""schemaVersion"": 1,
            ""listener"": {
                ""listenAddress"": ""192.168.1.10"",
                ""port"": 3128
            },
            ""allowedClients"": []
        }";
        await File.WriteAllTextAsync(_testConfigPath, v1Json);

        var config = await _manager.LoadAsync();

        config.SchemaVersion.Should().Be(2);
        config.Listener.Mode.Should().Be(ListenerMode.SpecificAddress);
        config.Listener.ListenAddress.Should().Be("192.168.1.10");
        
        config.ClientAccess.Mode.Should().Be(ClientAccessMode.AllowList);
        config.ClientAccess.AllowedClients.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SchemaV1_MigratesPopulatedClientsToAllowList()
    {
        var v1Json = @"
        {
            ""schemaVersion"": 1,
            ""listener"": {
                ""listenAddress"": ""127.0.0.1""
            },
            ""allowedClients"": [
                {
                    ""id"": ""client-1"",
                    ""name"": ""Test Client"",
                    ""ipOrCidr"": ""192.168.1.0/24"",
                    ""enabled"": true
                }
            ]
        }";
        await File.WriteAllTextAsync(_testConfigPath, v1Json);

        var config = await _manager.LoadAsync();

        config.SchemaVersion.Should().Be(2);
        config.Listener.Mode.Should().Be(ListenerMode.SpecificAddress);
        config.Listener.ListenAddress.Should().Be("127.0.0.1");
        
        config.ClientAccess.Mode.Should().Be(ClientAccessMode.AllowList);
        config.ClientAccess.AllowedClients.Should().HaveCount(1);
        config.ClientAccess.AllowedClients[0].Name.Should().Be("Test Client");
    }

    [Fact]
    public async Task SaveAsync_WhenFileIsReadOnly_ClearsReadOnlyAttributeAndSucceeds()
    {
        var initialConfig = new ProxyConfiguration { SchemaVersion = 2 };
        await _manager.SaveAsync(initialConfig);

        // Mark the file read-only
        File.SetAttributes(_testConfigPath, FileAttributes.ReadOnly);

        initialConfig.Listener.Port = 9090;
        await _manager.SaveAsync(initialConfig);

        var loaded = await _manager.LoadAsync();
        loaded.Listener.Port.Should().Be(9090);

        // Cleanup
        if (File.Exists(_testConfigPath))
        {
            File.SetAttributes(_testConfigPath, FileAttributes.Normal);
            File.Delete(_testConfigPath);
        }
    }
}
