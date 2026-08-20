using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Infrastructure.Platform;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests.Platform;

public class PersistentProxyInstanceProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IPlatformPathProvider> _mockPathProvider = new();

    public PersistentProxyInstanceProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PrintPilotProxy_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _mockPathProvider.Setup(p => p.DataDirectory).Returns(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void GetInstanceId_FirstCall_GeneratesAndSavesGuid()
    {
        var provider = new PersistentProxyInstanceProvider(_mockPathProvider.Object, NullLogger<PersistentProxyInstanceProvider>.Instance);

        var instanceId = provider.GetInstanceId();

        instanceId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(instanceId, out _).Should().BeTrue();

        var filePath = Path.Combine(_tempDir, "instance.id");
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath).Trim().Should().Be(instanceId);
    }

    [Fact]
    public void GetInstanceId_SubsequentInstance_ReadsExistingId()
    {
        var provider1 = new PersistentProxyInstanceProvider(_mockPathProvider.Object, NullLogger<PersistentProxyInstanceProvider>.Instance);
        var instanceId1 = provider1.GetInstanceId();

        // New provider instance representing application restart
        var provider2 = new PersistentProxyInstanceProvider(_mockPathProvider.Object, NullLogger<PersistentProxyInstanceProvider>.Instance);
        var instanceId2 = provider2.GetInstanceId();

        instanceId2.Should().Be(instanceId1);
    }

    [Fact]
    public void MultipleProxyInstances_DifferentDataDirs_HaveDistinctIds()
    {
        var tempDir2 = Path.Combine(Path.GetTempPath(), "PrintPilotProxy_Test_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir2);
            var mockPathProvider2 = new Mock<IPlatformPathProvider>();
            mockPathProvider2.Setup(p => p.DataDirectory).Returns(tempDir2);

            var provider1 = new PersistentProxyInstanceProvider(_mockPathProvider.Object);
            var provider2 = new PersistentProxyInstanceProvider(mockPathProvider2.Object);

            var id1 = provider1.GetInstanceId();
            var id2 = provider2.GetInstanceId();

            id1.Should().NotBe(id2);
        }
        finally
        {
            try { Directory.Delete(tempDir2, true); } catch { }
        }
    }
}
