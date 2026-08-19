using System;
using System.Linq;
using FluentAssertions;
using PrintPilotProxy.App.ViewModels;
using Xunit;

namespace PrintPilotProxy.App.Tests;

public class LogsViewModelTests
{
    [Fact]
    public void ParseLogEntries_WithStandardSerilogLines_ParsesCorrectlyAndReversesOrder()
    {
        // Arrange
        var logText =
            "2026-08-17 17:30:08.066 +03:00 [INF] Starting PrintPilotProxy Service...\r\n" +
            "2026-08-17 17:30:08.371 +03:00 [INF] PrintPilotProxy service worker starting.\r\n" +
            "2026-08-17 17:30:08.851 +03:00 [WRN] Could not inspect Windows Firewall status.\r\n";

        // Act
        var entries = LogsViewModel.ParseLogEntries(logText);

        // Assert: Newest entry must be first
        entries.Should().HaveCount(3);
        entries[0].Level.Should().Be("WRN");
        entries[0].Message.Should().Be("Could not inspect Windows Firewall status.");
        entries[0].Timestamp.Should().Be("2026-08-17 17:30:08.851");
        entries[0].LevelBrushKey.Should().Be("WarningBrush");

        entries[1].Message.Should().Be("PrintPilotProxy service worker starting.");
        entries[1].Level.Should().Be("INF");

        entries[2].Message.Should().Be("Starting PrintPilotProxy Service...");
        entries[2].Level.Should().Be("INF");
    }

    [Fact]
    public void ParseLogEntries_WithMultilineException_AttachesExceptionToEntry()
    {
        // Arrange
        var logText =
            "2026-08-17 17:30:08.066 +03:00 [INF] Starting PrintPilotProxy Service...\r\n" +
            "2026-08-17 17:30:08.851 +03:00 [ERR] Configuration apply failed.\r\n" +
            "System.IO.FileNotFoundException: The system cannot find the file specified.\r\n" +
            "   at PrintPilotProxy.Service.ProxyWorker.ValidateRuntimeConfigurationAsync()\r\n" +
            "   at PrintPilotProxy.Service.ProxyWorker.HandleUpdateConfigurationAsync()\r\n" +
            "2026-08-17 17:30:09.000 +03:00 [INF] Next operation.\r\n";

        // Act
        var entries = LogsViewModel.ParseLogEntries(logText);

        // Assert
        entries.Should().HaveCount(3);
        
        // Newest entry is the last line: Next operation
        entries[0].Message.Should().Be("Next operation.");
        entries[0].HasDetails.Should().BeFalse();

        // Second entry is the error with exception stack trace
        entries[1].Level.Should().Be("ERR");
        entries[1].Message.Should().Be("Configuration apply failed.");
        entries[1].HasDetails.Should().BeTrue();
        entries[1].Details.Should().Contain("FileNotFoundException");
        entries[1].Details.Should().Contain("ProxyWorker.ValidateRuntimeConfigurationAsync");

        // Third entry is the first starting line
        entries[2].Message.Should().Be("Starting PrintPilotProxy Service...");
    }

    [Fact]
    public void ParseLogEntries_EmptyOrWhitespace_ReturnsEmptyList()
    {
        LogsViewModel.ParseLogEntries("").Should().BeEmpty();
        LogsViewModel.ParseLogEntries("   \r\n \n").Should().BeEmpty();
    }
}
