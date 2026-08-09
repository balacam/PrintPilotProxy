using System.IO;
using PrintPilotProxy.Core.Interfaces;

namespace PrintPilotProxy.Infrastructure.Platform
{
    public class WindowsPathProvider : IPlatformPathProvider
    {
        private const string BaseDir = @"C:\ProgramData\PrintPilotProxy";

        public string ConfigurationDirectory => BaseDir;
        public string LogDirectory => Path.Combine(BaseDir, "logs");
        public string DataDirectory => Path.Combine(BaseDir, "data");
        public string BackupDirectory => Path.Combine(BaseDir, "backups");
        public string ConfigurationFilePath => Path.Combine(ConfigurationDirectory, "config.json");

        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(ConfigurationDirectory);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
        }
    }
}
