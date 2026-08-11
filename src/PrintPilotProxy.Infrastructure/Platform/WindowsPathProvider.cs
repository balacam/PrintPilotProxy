using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
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
            EnsureDirectoryWithPermissions(ConfigurationDirectory);
            EnsureDirectoryWithPermissions(LogDirectory);
            EnsureDirectoryWithPermissions(DataDirectory);
            EnsureDirectoryWithPermissions(BackupDirectory);

            EnsureFileNotReadOnly(ConfigurationFilePath);
        }

        private static void EnsureDirectoryWithPermissions(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var directoryInfo = new DirectoryInfo(path);
                    var directorySecurity = directoryInfo.GetAccessControl();

                    var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                    var rule = new FileSystemAccessRule(
                        usersSid,
                        FileSystemRights.Modify | FileSystemRights.Synchronize,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow);

                    directorySecurity.AddAccessRule(rule);
                    directoryInfo.SetAccessControl(directorySecurity);
                }
                catch
                {
                    // Ignore ACL setting errors if process lacks permission to modify security descriptor
                }
            }
        }

        private static void EnsureFileNotReadOnly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                    }
                }
            }
            catch
            {
                // Best-effort attribute clearance
            }
        }
    }
}
