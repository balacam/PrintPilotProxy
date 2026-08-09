using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PrintPilotProxy.Core.Interfaces;
using PrintPilotProxy.Core.Models;
using PrintPilotProxy.Core.Validation;

namespace PrintPilotProxy.Infrastructure.Configuration
{
    public class JsonConfigurationManager : IConfigurationManager
    {
        private readonly IPlatformPathProvider _pathProvider;
        private readonly ILogger<JsonConfigurationManager> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonConfigurationManager(
            IPlatformPathProvider pathProvider,
            ILogger<JsonConfigurationManager> logger)
        {
            _pathProvider = pathProvider;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<ProxyConfiguration> LoadAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                string filePath = _pathProvider.ConfigurationFilePath;
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation("Configuration file not found, creating default.");
                    var defaultConfig = new ProxyConfiguration();
                    await SaveInternalAsync(defaultConfig, cancellationToken);
                    return defaultConfig;
                }

                string json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var config = JsonSerializer.Deserialize<ProxyConfiguration>(json, _jsonOptions);
                return config ?? new ProxyConfiguration();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration.");
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SaveAsync(ProxyConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await SaveInternalAsync(configuration, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task SaveInternalAsync(ProxyConfiguration configuration, CancellationToken cancellationToken)
        {
            _pathProvider.EnsureDirectoriesExist();
            string json = JsonSerializer.Serialize(configuration, _jsonOptions);
            await File.WriteAllTextAsync(_pathProvider.ConfigurationFilePath, json, cancellationToken);
            _logger.LogInformation("Configuration saved successfully.");
        }

        public IReadOnlyList<string> Validate(ProxyConfiguration configuration)
        {
            return ConfigurationValidator.Validate(configuration);
        }

        public async Task<string> BackupAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _pathProvider.EnsureDirectoriesExist();
                string sourcePath = _pathProvider.ConfigurationFilePath;
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException("Configuration file not found to backup.");
                }

                string fileName = $"config_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string destPath = Path.Combine(_pathProvider.BackupDirectory, fileName);
                
                File.Copy(sourcePath, destPath, overwrite: true);
                _logger.LogInformation("Configuration backed up to {Path}", destPath);
                return destPath;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ProxyConfiguration> RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
                }

                string json = await File.ReadAllTextAsync(backupFilePath, cancellationToken);
                var config = JsonSerializer.Deserialize<ProxyConfiguration>(json, _jsonOptions) 
                    ?? throw new InvalidOperationException("Failed to deserialize backup configuration.");

                var validationResult = Validate(config);
                if (validationResult.Count > 0)
                {
                    throw new InvalidOperationException($"Backup configuration is invalid: {string.Join(", ", validationResult)}");
                }

                await SaveInternalAsync(config, cancellationToken);
                _logger.LogInformation("Configuration restored successfully from {Path}", backupFilePath);
                return config;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public IReadOnlyList<string> GetBackups()
        {
            _pathProvider.EnsureDirectoriesExist();
            return Directory.GetFiles(_pathProvider.BackupDirectory, "*.json")
                            .OrderByDescending(f => f)
                            .ToList();
        }

        public async Task ExportAsync(string exportPath, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                string sourcePath = _pathProvider.ConfigurationFilePath;
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException("Configuration file not found to export.");
                }

                File.Copy(sourcePath, exportPath, overwrite: true);
                _logger.LogInformation("Configuration exported to {Path}", exportPath);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<ProxyConfiguration> ImportAsync(string importPath, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!File.Exists(importPath))
                {
                    throw new FileNotFoundException("Import file not found.");
                }

                string json = await File.ReadAllTextAsync(importPath, cancellationToken);
                var config = JsonSerializer.Deserialize<ProxyConfiguration>(json, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize import configuration.");
                
                var validationResult = Validate(config);
                if (validationResult.Count > 0)
                {
                    throw new InvalidOperationException($"Imported configuration is invalid: {string.Join(", ", validationResult)}");
                }

                await SaveInternalAsync(config, cancellationToken);
                _logger.LogInformation("Configuration imported from {Path}", importPath);
                return config;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
