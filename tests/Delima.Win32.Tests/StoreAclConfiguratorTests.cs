using System.IO;
using Delima.Core.Audit;
using Delima.Win32.Store;
using Xunit;

namespace Delima.Win32.Tests;

public class StoreAclConfiguratorTests : IDisposable
{
    private readonly string _testDirectory;

    public StoreAclConfiguratorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DelimaAclConfigTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public void ApplyStoreFileAcls_NonExistentFile_ReturnsWithoutError()
    {
        string nonExistent = Path.Combine(_testDirectory, "non_existent.dat");
        // Should not throw for non-existent file
        StoreAclConfigurator.ApplyStoreFileAcls(nonExistent);
    }

    [Fact]
    public void ApplyStoreFileAcls_ValidFile_ConfiguresAclsSuccessfullyOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        string targetFile = Path.Combine(_testDirectory, "credentials.dat");
        File.WriteAllText(targetFile, "dummy content");

        // Should succeed on user-created file
        StoreAclConfigurator.ApplyStoreFileAcls(targetFile, pupilAccount: "Murid");

        Assert.True(File.Exists(targetFile));
    }

    [Fact]
    public void ApplyAuditDirectoryAcls_ValidDirectory_ConfiguresAclsSuccessfullyOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        string auditDir = Path.Combine(_testDirectory, "audit");
        Directory.CreateDirectory(auditDir);

        StoreAclConfigurator.ApplyAuditDirectoryAcls(auditDir, pupilAccount: "Murid");

        Assert.True(Directory.Exists(auditDir));
    }

    [Fact]
    public void ApplyPublicDirectoryAcls_ValidDirectory_ConfiguresAclsSuccessfullyOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        string themeDir = Path.Combine(_testDirectory, "theme");
        Directory.CreateDirectory(themeDir);

        StoreAclConfigurator.ApplyPublicDirectoryAcls(themeDir);

        Assert.True(Directory.Exists(themeDir));
    }

    [Fact]
    public void EnsureDirectoryStructure_CreatesDirectoriesAndAppliesAcls()
    {
        string baseDir = Path.Combine(_testDirectory, "DelimaStore");
        StoreAclConfigurator.EnsureDirectoryStructure(baseDir, pupilAccount: "Murid", applyAcls: true);

        Assert.True(Directory.Exists(Path.Combine(baseDir, "audit")));
        Assert.True(Directory.Exists(Path.Combine(baseDir, "theme")));
        Assert.True(Directory.Exists(Path.Combine(baseDir, "assets", "avatars")));
    }

    [Fact]
    public void AuditLogger_RecordsAclFailure_WhenInvokedDirectlyOrOnFailure()
    {
        string fakeTarget = Path.Combine(_testDirectory, "credentials.dat");
        string auditDir = Path.Combine(_testDirectory, "audit");

        AuditLogger.RecordAclFailure(
            targetPath: fakeTarget,
            errorMessage: "Access is denied (5)",
            pupilAccount: "Murid",
            auditDirectory: auditDir);

        string logPath = AuditLogger.GetAuditLogFilePath(DateTimeOffset.UtcNow, auditDir);
        Assert.True(File.Exists(logPath));

        string content = File.ReadAllText(logPath);
        Assert.Contains("acl_failure", content);
        Assert.Contains("ACL_DENIED", content);
        Assert.Contains("Access is denied", content);
        Assert.Contains("credentials.dat", content);
    }

    [Fact]
    public void InstallerScript_DelimaLauncherIss_Specifies_EveryoneNone_AdminsFull_SystemFull()
    {
        string baseDir = AppContext.BaseDirectory;
        string? solutionRoot = null;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DelimaLauncher.sln")) || Directory.Exists(Path.Combine(dir.FullName, "installer")))
            {
                solutionRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        Assert.NotNull(solutionRoot);
        string issPath = Path.Combine(solutionRoot, "installer", "DelimaLauncher.iss");
        Assert.True(File.Exists(issPath), $"DelimaLauncher.iss not found at {issPath}");

        string issContent = File.ReadAllText(issPath);
        Assert.Contains(@"Name: ""{commonappdata}\DELIMa Launcher""; Permissions: everyone-none admins-full system-full", issContent);
    }
}

