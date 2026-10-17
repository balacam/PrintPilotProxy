using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Configuration;
using PrintPilotProxy.Infrastructure.Ipc;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Configuration;

/// <summary>PROXY-009.</summary>
public class IpcRetryAndBackupTests
{
    [Theory]
    [InlineData(IpcMessageTypes.GetStatus, true)]
    [InlineData(IpcMessageTypes.GetConfiguration, true)]
    [InlineData(IpcMessageTypes.GetFirewallStatus, true)]
    [InlineData(IpcMessageTypes.UpdateConfiguration, false)]
    [InlineData(IpcMessageTypes.RestartProxy, false)]
    [InlineData(IpcMessageTypes.ApplyFirewallRule, false)]
    [InlineData("SomethingNew", false)]
    public void OnlyReadCommandsAreRetried(string type, bool expected)
    {
        NamedPipeIpcClient.IsReadOnlyCommand(type).Should().Be(expected);
    }

    [Fact]
    public async Task Backups_TakenInTheSameInstant_DoNotOverwriteEachOther()
    {
        var root = Path.Combine(Path.GetTempPath(), $"backup_test_{Guid.NewGuid()}");
        var backups = Path.Combine(root, "backups");
        Directory.CreateDirectory(backups);
        var configPath = Path.Combine(root, "config.json");
        await File.WriteAllTextAsync(configPath, "{\"schemaVersion\":2}");
        var paths = new Mock<IPlatformPathProvider>();
        paths.Setup(p => p.ConfigurationFilePath).Returns(configPath);
        paths.Setup(p => p.BackupDirectory).Returns(backups);
        var manager = new JsonConfigurationManager(paths.Object, NullLogger<JsonConfigurationManager>.Instance);

        var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => manager.BackupAsync()));

        created.Distinct().Should().HaveCount(8);
        Directory.GetFiles(backups).Should().HaveCount(8);
        Directory.Delete(root, recursive: true);
    }
}
