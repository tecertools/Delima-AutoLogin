using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Delima.Core.Audit;

namespace Delima.Win32.Store;

/// <summary>
/// Configures Windows file and directory ACLs for the DELIMa Launcher credential store
/// and related assets per Technical Architecture §3.5.
/// </summary>
public static class StoreAclConfigurator
{
    public const string DefaultPupilAccount = "Murid";

    /// <summary>
    /// Applies strict ACLs to store files (credentials.dat, credentials.entropy):
    /// SYSTEM: FullControl, Administrators: FullControl, Murid: Read.
    /// Inheritance is disabled and all other users are denied access.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static void ApplyStoreFileAcls(string filePath, string pupilAccount = DefaultPupilAccount)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!File.Exists(filePath)) return;

        var fileInfo = new FileInfo(filePath);
        var fileSecurity = new FileSecurity();

        // Disable inheritance and remove existing inherited rules
        fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        // SYSTEM: FullControl
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(
            systemSid,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        // Administrators: FullControl
        var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(
            adminSid,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        // Pupil account: Read access
        if (TryResolveSid(pupilAccount, out var pupilSid))
        {
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                pupilSid,
                FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
                AccessControlType.Allow));
        }

        try
        {
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null && !currentUser.Equals(systemSid) && !currentUser.Equals(adminSid) && (pupilSid == null || !currentUser.Equals(pupilSid)))
            {
                fileSecurity.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
                    AccessControlType.Allow));
            }
        }
        catch (Exception ex)
        {
            string auditDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? DpapiCredentialStore.GetDefaultStoreDirectory(), "audit");
            AuditLogger.RecordWarning($"Failed to resolve or add current user identity rule to ACL for '{filePath}': {ex.Message}", filePath, auditDir);
        }

        try
        {
            fileInfo.SetAccessControl(fileSecurity);
        }
        catch (Exception ex)
        {
            string auditDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? DpapiCredentialStore.GetDefaultStoreDirectory(), "audit");
            AuditLogger.RecordAclFailure(filePath, ex.Message, pupilAccount, auditDir);
            throw new UnauthorizedAccessException($"Failed to apply security ACLs to store file '{filePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies ACLs to the audit directory per §3.5:
    /// SYSTEM: FullControl, Administrators: FullControl, Murid: Append-only write (no delete).
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static void ApplyAuditDirectoryAcls(string auditDirectoryPath, string pupilAccount = DefaultPupilAccount)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!Directory.Exists(auditDirectoryPath))
        {
            Directory.CreateDirectory(auditDirectoryPath);
        }

        var dirInfo = new DirectoryInfo(auditDirectoryPath);
        var dirSecurity = new DirectorySecurity();

        dirSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        dirSecurity.AddAccessRule(new FileSystemAccessRule(
            systemSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        dirSecurity.AddAccessRule(new FileSystemAccessRule(
            adminSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        if (TryResolveSid(pupilAccount, out var pupilSid))
        {
            // Murid: Write (append-only, no delete) + Read
            FileSystemRights appendOnlyRights =
                FileSystemRights.CreateFiles |
                FileSystemRights.AppendData |
                FileSystemRights.WriteAttributes |
                FileSystemRights.WriteExtendedAttributes |
                FileSystemRights.ReadData |
                FileSystemRights.ReadAttributes |
                FileSystemRights.ReadExtendedAttributes |
                FileSystemRights.ReadPermissions |
                FileSystemRights.Synchronize;

            dirSecurity.AddAccessRule(new FileSystemAccessRule(
                pupilSid,
                appendOnlyRights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        try
        {
            dirInfo.SetAccessControl(dirSecurity);
        }
        catch (Exception ex)
        {
            AuditLogger.RecordAclFailure(auditDirectoryPath, ex.Message, pupilAccount, auditDirectoryPath);
            throw new UnauthorizedAccessException($"Failed to apply security ACLs to audit directory '{auditDirectoryPath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies ACLs to public directories (theme, assets/avatars) per §3.5:
    /// SYSTEM: FullControl, Administrators: FullControl, Builtin Users: Read.
    /// </summary>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public static void ApplyPublicDirectoryAcls(string publicDirectoryPath)
    {
        if (!OperatingSystem.IsWindows()) return;
        if (!Directory.Exists(publicDirectoryPath))
        {
            Directory.CreateDirectory(publicDirectoryPath);
        }

        var dirInfo = new DirectoryInfo(publicDirectoryPath);
        var dirSecurity = new DirectorySecurity();

        dirSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        dirSecurity.AddAccessRule(new FileSystemAccessRule(
            systemSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        dirSecurity.AddAccessRule(new FileSystemAccessRule(
            adminSid,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        dirSecurity.AddAccessRule(new FileSystemAccessRule(
            usersSid,
            FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        try
        {
            dirInfo.SetAccessControl(dirSecurity);
        }
        catch (Exception ex)
        {
            string auditDir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(publicDirectoryPath)) ?? DpapiCredentialStore.GetDefaultStoreDirectory(), "audit");
            AuditLogger.RecordAclFailure(publicDirectoryPath, ex.Message, pupilAccount: null, auditDir);
            throw new UnauthorizedAccessException($"Failed to apply security ACLs to public directory '{publicDirectoryPath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ensures the full DELIMa directory structure exists and applies all ACLs from §3.5.
    /// </summary>
    public static void EnsureDirectoryStructure(string baseDirectory, string pupilAccount = DefaultPupilAccount, bool applyAcls = true)
    {
        Directory.CreateDirectory(baseDirectory);

        string auditDir = Path.Combine(baseDirectory, "audit");
        string themeDir = Path.Combine(baseDirectory, "theme");
        string avatarsDir = Path.Combine(baseDirectory, "assets", "avatars");

        Directory.CreateDirectory(auditDir);
        Directory.CreateDirectory(themeDir);
        Directory.CreateDirectory(avatarsDir);

        if (applyAcls && OperatingSystem.IsWindows())
        {
            ApplyAuditDirectoryAcls(auditDir, pupilAccount);
            ApplyPublicDirectoryAcls(themeDir);
            ApplyPublicDirectoryAcls(avatarsDir);
        }
    }

    private static bool TryResolveSid(string accountName, [NotNullWhen(true)] out SecurityIdentifier? sid)
    {
        sid = null;
        if (string.IsNullOrWhiteSpace(accountName)) return false;

        try
        {
            var ntAccount = new NTAccount(accountName);
            sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
            return true;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
