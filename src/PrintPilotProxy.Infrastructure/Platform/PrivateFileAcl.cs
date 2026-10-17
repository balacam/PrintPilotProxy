using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace PrintPilotProxy.Infrastructure.Platform;

/// <summary>
/// Restricts a file or folder that holds secrets (the configuration with its protected upstream password, and its
/// backups) to SYSTEM and Administrators. Inheritance is cut and every existing entry, including legacy explicit
/// ones, is dropped, so a previously broad "Users" grant cannot survive.
/// </summary>
public static class PrivateFileAcl
{
    public static bool Apply(string path, ILogger? logger = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            // The process doing the write keeps access to what it just created (a non-elevated process could not
            // otherwise rename its own temp file). In the service this is SYSTEM, so nothing broader is granted.
            var self = WindowsIdentity.GetCurrent().User;
            var extra = self is not null && !self.Equals(system) && !self.Equals(admins) ? self : null;

            if (Directory.Exists(path))
            {
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                var flags = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, flags, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, flags, PropagationFlags.None, AccessControlType.Allow));
                if (extra is not null)
                {
                    security.AddAccessRule(new FileSystemAccessRule(extra, FileSystemRights.FullControl, flags, PropagationFlags.None, AccessControlType.Allow));
                }
                new DirectoryInfo(path).SetAccessControl(security);
                return true;
            }

            if (File.Exists(path))
            {
                var security = new FileSecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
                if (extra is not null)
                {
                    security.AddAccessRule(new FileSystemAccessRule(extra, FileSystemRights.FullControl, AccessControlType.Allow));
                }
                new FileInfo(path).SetAccessControl(security);
                return true;
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not restrict access to {Path}; it keeps its inherited permissions.", path);
        }

        return false;
    }
}
