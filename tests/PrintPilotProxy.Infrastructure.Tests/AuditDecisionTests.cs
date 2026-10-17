using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Infrastructure.Configuration;
using PrintPilotProxy.Infrastructure.Ipc;
using PrintPilotProxy.Infrastructure.Platform;
using Xunit;

namespace PrintPilotProxy.Infrastructure.Tests;

/// <summary>PROXY-002, 004, 006, 009.</summary>
public class AuditDecisionTests
{
    // PROXY-006: the console user may manage the proxy by default; one setting requires an administrator.
    [Fact]
    public void ConsoleUser_MayManageByDefault()
    {
        var validator = new IpcSecurityValidator(NullLogger<IpcSecurityValidator>.Instance);
        var consoleUser = IpcClientIdentity.CreateInteractiveUser();

        validator.IsAuthorized(consoleUser, IpcMessageTypes.UpdateConfiguration, out _).Should().BeTrue();
    }

    [Fact]
    public void ConsoleUser_IsDenied_WhenAdministratorIsRequired()
    {
        var validator = new IpcSecurityValidator(NullLogger<IpcSecurityValidator>.Instance, new IpcSecurityOptions { RequireAdministrator = true });
        var consoleUser = IpcClientIdentity.CreateInteractiveUser();

        validator.IsAuthorized(consoleUser, IpcMessageTypes.UpdateConfiguration, out var reason).Should().BeFalse();
        reason.Should().Contain("not authorized");
        validator.IsAuthorized(consoleUser, IpcMessageTypes.GetStatus, out _).Should().BeTrue();
    }

    [Fact]
    public void Administrator_StillAllowed_WhenAdministratorIsRequired()
    {
        var validator = new IpcSecurityValidator(NullLogger<IpcSecurityValidator>.Instance, new IpcSecurityOptions { RequireAdministrator = true });
        var admin = new IpcClientIdentity { IsAdministrator = true, Name = "admin" };

        validator.IsAuthorized(admin, IpcMessageTypes.StopProxy, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("nonsense", false)]
    [InlineData(null, false)]
    public void Option_ReadsTheConfigurationKey(string? value, bool expected)
    {
        var configuration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configuration.Setup(c => c["Ipc:RequireAdministrator"]).Returns(value);

        IpcSecurityOptions.FromConfiguration(configuration.Object).RequireAdministrator.Should().Be(expected);
    }

    // PROXY-009: a repeated command gets the earlier answer; the cache is bounded in size and age.
    [Fact]
    public void RecentCommandResults_ReturnsTheEarlierAnswerForTheSameCorrelationId()
    {
        var cache = new RecentCommandResults();
        var first = new IpcMessage { Type = IpcMessageTypes.StatusResponse, CorrelationId = "abc", Payload = "applied" };

        cache.Store("abc", IpcMessageTypes.UpdateConfiguration, first);

        cache.TryGet("abc", IpcMessageTypes.UpdateConfiguration, out var again).Should().BeTrue();
        again.Should().BeSameAs(first);
        cache.TryGet("abc", IpcMessageTypes.StopProxy, out _).Should().BeFalse("a different command is not a repeat");
        cache.TryGet("other", IpcMessageTypes.UpdateConfiguration, out _).Should().BeFalse();
        cache.TryGet(null, IpcMessageTypes.UpdateConfiguration, out _).Should().BeFalse();
    }

    [Fact]
    public void RecentCommandResults_ForgetsOldEntriesAndStaysBounded()
    {
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        var cache = new RecentCommandResults(capacity: 2, ttl: TimeSpan.FromMinutes(1), clock: () => now);
        var message = new IpcMessage { Type = IpcMessageTypes.StatusResponse };

        cache.Store("a", "T", message);
        now = now.AddSeconds(1);
        cache.Store("b", "T", message);
        now = now.AddSeconds(1);
        cache.Store("c", "T", message);

        cache.TryGet("a", "T", out _).Should().BeFalse("the oldest entry is dropped at capacity");
        cache.TryGet("b", "T", out _).Should().BeTrue();
        cache.TryGet("c", "T", out _).Should().BeTrue();

        now = now.AddMinutes(2);
        cache.TryGet("b", "T", out _).Should().BeFalse("entries expire");
    }

    // PROXY-004 / PROXY-017: secret-bearing files are limited to SYSTEM and Administrators.
    [Fact]
    public void PrivateFileAcl_RemovesBroadAccessFromAFile()
    {
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Combine(Path.GetTempPath(), $"acl_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{}");
        try
        {
            PrivateFileAcl.Apply(path).Should().BeTrue();

            var security = new FileInfo(path).GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            var sids = security.GetAccessRules(true, false, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Select(r => r.IdentityReference.Value)
                .ToList();
            sids.Should().Contain(new[] { "S-1-5-18", "S-1-5-32-544" });
            // Only the creating process may be added beside them; never Users, Everyone or Authenticated Users.
            sids.Should().NotContain(new[] { "S-1-5-32-545", "S-1-1-0", "S-1-5-11" });
            sids.Count.Should().BeLessOrEqualTo(3);
        }
        finally
        {
            try
            {
                // The file is now private; give the current user back control just to remove it.
                var cleanup = new FileSecurity();
                cleanup.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, AccessControlType.Allow));
                new FileInfo(path).SetAccessControl(cleanup);
                File.Delete(path);
            }
            catch { }
        }
    }

    // PROXY-002: a brand-new installation starts closed; an existing file is not changed.
    [Fact]
    public async Task FirstRunConfiguration_StartsClosed_ButExistingFilesKeepTheirMode()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"firstrun_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var configPath = Path.Combine(dir, "config.json");
        var backups = Path.Combine(dir, "backups");
        Directory.CreateDirectory(backups);
        var paths = new Mock<IPlatformPathProvider>();
        paths.Setup(p => p.ConfigurationFilePath).Returns(configPath);
        paths.Setup(p => p.BackupDirectory).Returns(backups);
        var manager = new JsonConfigurationManager(paths.Object, NullLogger<JsonConfigurationManager>.Instance);
        try
        {
            var fresh = await manager.LoadAsync();
            fresh.ClientAccess.Mode.Should().Be(ClientAccessMode.AllowList);
            fresh.ClientAccess.AllowedClients.Should().BeEmpty();

            await File.WriteAllTextAsync(configPath, "{\"schemaVersion\":2,\"clientAccess\":{\"mode\":\"allowAll\"}}");
            var existing = await manager.LoadAsync();
            existing.ClientAccess.Mode.Should().Be(ClientAccessMode.AllowAll);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
