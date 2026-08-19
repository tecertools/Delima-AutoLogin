using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Delima.Core.Crypto;
using Delima.Core.Store;
using Delima.Provision;
using Delima.Win32.Store;

namespace Delima.Win32.Tests;

public class ProvisionEngineTests : IDisposable
{
    private readonly string _testDirectory;

    public ProvisionEngineTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DelimaProvisionTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore temp cleanup errors
        }
    }

    private static MasterBundlePayload CreateSamplePayload(string schoolCode = "SKS24", int pupilCount = 5)
    {
        var payload = new MasterBundlePayload
        {
            SchemaVersion = 2,
            School = new SchoolInfo
            {
                Code = schoolCode,
                Name = "Sekolah Kebangsaan Seksyen 24",
                Motto = "Berilmu Berbakti",
                Domain = "moe-dl.edu.my"
            },
            Theme = new ThemeInfo
            {
                Primary = "#056839",
                Accent = "#F7941D",
                ClassColours = ["#C41118", "#056839"]
            },
            Config = new AppConfig
            {
                PicturePasswordRequired = true,
                IdleResetSeconds = 600,
                InjectionSettleMs = 400,
                WindowWaitTimeoutMs = 30000,
                StoreMaxAgeDays = 30
            },
            GeneratedAt = DateTimeOffset.UtcNow,
            Classes = [new ClassInfo { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 }],
            Students = []
        };

        for (int i = 1; i <= pupilCount; i++)
        {
            payload.Students.Add(new StudentInfo
            {
                Id = $"s_{i:D4}",
                Name = $"Murid Ujian {i}",
                ClassId = "2_cemerlang",
                EmailLocal = $"m-{10000000 + i}",
                Avatar = "kucing",
                Password = $"Password{i}#2026",
                PasswordVersion = 1,
                Active = true,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        return payload;
    }

    private string CreateSampleBundle(string passphrase, MasterBundlePayload? payload = null)
    {
        payload ??= CreateSamplePayload();
        byte[] bundleBytes = MasterBundle.Pack(payload, passphrase, Argon2Parameters.FastTest);

        string bundlePath = Path.Combine(_testDirectory, "school.dlmpack");
        File.WriteAllBytes(bundlePath, bundleBytes);
        return bundlePath;
    }

    [Fact]
    public void Execute_WithValidBundleAndPassphraseStdin_SuccessfullyProvisionsStore()
    {
        // Arrange
        const string passphrase = "AdminPassword#2026";
        string bundlePath = CreateSampleBundle(passphrase);
        string targetDir = Path.Combine(_testDirectory, "StoreOutput");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            ApplyAcls = false
        };

        using var inReader = new StringReader(passphrase + "\r\n");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("SKS24", result.SchoolCode);
        Assert.Equal(5, result.StudentCount);
        Assert.NotNull(result.DeviceId);

        // Verify files written to disk
        Assert.True(File.Exists(Path.Combine(targetDir, DpapiCredentialStore.CredentialsFileName)));
        Assert.True(File.Exists(Path.Combine(targetDir, DpapiCredentialStore.EntropyFileName)));
        Assert.True(File.Exists(Path.Combine(targetDir, "device.id")));
        Assert.True(File.Exists(Path.Combine(targetDir, "provision.json")));

        // Verify DPAPI store is openable and valid
        using var store = DpapiCredentialStore.Open(targetDir);
        Assert.Equal(5, store.StudentCount);
        Assert.Equal("SKS24", store.SchoolCode);
        Assert.True(store.HasCredential("s_0001"));

        using var cred = store.OpenCredential("s_0001");
        Assert.Equal("Password1#2026", cred.PasswordSpan.ToString());
    }

    [Fact]
    public void Execute_WithPassphraseStdin_TrimsLineEndingsCorrectly()
    {
        // Arrange
        const string passphrase = "MySecretPassphrase!";
        string bundlePath = CreateSampleBundle(passphrase);
        string targetDir = Path.Combine(_testDirectory, "Store_TrimTest");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            ApplyAcls = false
        };

        // Passphrase with trailing LF only
        using var inReader = new StringReader(passphrase + "\n");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Execute_WithWrongPassphrase_ReturnsAuthenticationFailedExitCode()
    {
        // Arrange
        string bundlePath = CreateSampleBundle("CorrectPassphrase");
        string targetDir = Path.Combine(_testDirectory, "Store_WrongPass");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            ApplyAcls = false
        };

        using var inReader = new StringReader("WrongPassphrase\r\n");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ProvisionEngine.ExitAuthenticationFailed, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(targetDir, DpapiCredentialStore.CredentialsFileName)));
    }

    [Fact]
    public void Execute_WithCorruptedBundle_ReturnsAuthenticationFailedExitCode()
    {
        // Arrange
        string bundlePath = CreateSampleBundle("AnyPassphrase");
        byte[] bytes = File.ReadAllBytes(bundlePath);
        // Tamper with ciphertext
        bytes[^20] ^= 0xFF;
        File.WriteAllBytes(bundlePath, bytes);

        string targetDir = Path.Combine(_testDirectory, "Store_Corrupt");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            ApplyAcls = false
        };

        using var inReader = new StringReader("AnyPassphrase\r\n");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ProvisionEngine.ExitAuthenticationFailed, result.ExitCode);
    }

    [Fact]
    public void Execute_WithNonExistentBundle_ReturnsNotFoundExitCode()
    {
        // Arrange
        string nonExistentPath = Path.Combine(_testDirectory, "does_not_exist.dlmpack");

        var options = new ProvisionOptions
        {
            PackPath = nonExistentPath,
            PassphraseStdin = true,
            Quiet = true,
            ApplyAcls = false
        };

        using var inReader = new StringReader("Passphrase\r\n");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ProvisionEngine.ExitInvalidArgsOrNotFound, result.ExitCode);
    }

    [Fact]
    public void Execute_WithEmptyStdinPassphrase_ReturnsInvalidArgsExitCode()
    {
        // Arrange
        string bundlePath = CreateSampleBundle("Passphrase");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            ApplyAcls = false
        };

        using var inReader = new StringReader("\r\n"); // empty line
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ProvisionEngine.ExitInvalidArgsOrNotFound, result.ExitCode);
    }

    [Fact]
    public void Execute_PreservesExistingDeviceIdOnSecondRun()
    {
        // Arrange
        const string passphrase = "AdminPassphrase123";
        string bundlePath = CreateSampleBundle(passphrase);
        string targetDir = Path.Combine(_testDirectory, "Store_DevicePreserve");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            ApplyAcls = false
        };

        // First run
        using (var inReader1 = new StringReader(passphrase + "\r\n"))
        using (var outWriter1 = new StringWriter())
        using (var errWriter1 = new StringWriter())
        {
            var result1 = ProvisionEngine.Execute(options, inReader1, outWriter1, errWriter1);
            Assert.True(result1.Success);
            Assert.NotNull(result1.DeviceId);

            string initialDeviceId = File.ReadAllText(Path.Combine(targetDir, "device.id")).Trim();
            Assert.Equal(result1.DeviceId, initialDeviceId);

            // Second run
            using var inReader2 = new StringReader(passphrase + "\r\n");
            using var outWriter2 = new StringWriter();
            using var errWriter2 = new StringWriter();

            var result2 = ProvisionEngine.Execute(options, inReader2, outWriter2, errWriter2);
            Assert.True(result2.Success);

            string secondDeviceId = File.ReadAllText(Path.Combine(targetDir, "device.id")).Trim();
            Assert.Equal(initialDeviceId, secondDeviceId);
            Assert.Equal(result1.DeviceId, result2.DeviceId);
        }
    }

    [Fact]
    public void Execute_AppendsToLabChecklist_WhenChecklistProvidedOrDiscovered()
    {
        // Arrange
        const string passphrase = "AdminPassphrase123";
        string bundlePath = CreateSampleBundle(passphrase);
        string checklistPath = Path.Combine(_testDirectory, "lab_checklist.csv");
        string targetDir = Path.Combine(_testDirectory, "Store_ChecklistTest");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            ChecklistPath = checklistPath,
            ApplyAcls = false
        };

        // Run 1
        using (var inReader = new StringReader(passphrase + "\r\n"))
        using (var outWriter = new StringWriter())
        using (var errWriter = new StringWriter())
        {
            var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);
            Assert.True(result.Success);
            Assert.True(result.ChecklistUpdated);
        }

        Assert.True(File.Exists(checklistPath));
        string[] lines = File.ReadAllLines(checklistPath);
        Assert.Equal(2, lines.Length); // Header + 1 record
        Assert.Contains("Timestamp,PC_Name,Device_ID,School_Code,Software_Version,Store_Date,Status", lines[0]);
        Assert.Contains("SKS24", lines[1]);
        Assert.Contains("SUCCESS", lines[1]);

        // Run 2 (second run appends another row, does not duplicate header)
        using (var inReader = new StringReader(passphrase + "\r\n"))
        using (var outWriter = new StringWriter())
        using (var errWriter = new StringWriter())
        {
            var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);
            Assert.True(result.Success);
            Assert.True(result.ChecklistUpdated);
        }

        lines = File.ReadAllLines(checklistPath);
        Assert.Equal(3, lines.Length); // Header + 2 records
    }

    [Fact]
    public void Execute_DryRun_ValidatesWithoutWritingToDisk()
    {
        // Arrange
        const string passphrase = "AdminPassphrase123";
        string bundlePath = CreateSampleBundle(passphrase);
        string targetDir = Path.Combine(_testDirectory, "Store_DryRun");

        var options = new ProvisionOptions
        {
            PackPath = bundlePath,
            PassphraseStdin = true,
            Quiet = true,
            TargetDirectory = targetDir,
            DryRun = true,
            ApplyAcls = false
        };

        using var inReader = new StringReader(passphrase + "\r\n");
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();

        // Act
        var result = ProvisionEngine.Execute(options, inReader, outWriter, errWriter);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(targetDir, DpapiCredentialStore.CredentialsFileName)));
        Assert.False(File.Exists(Path.Combine(targetDir, "device.id")));
    }

    [Fact]
    public void ProvisionOptions_Parsing_HandlesAllFlags()
    {
        string[] args =
        [
            "--pack", @"C:\dlm\school.dlmpack",
            "--quiet",
            "--passphrase-stdin",
            "--target-dir", @"C:\ProgramData\DELIMa",
            "--checklist", @"\\share\lab_checklist.csv",
            "--pupil-account", "Pelajar",
            "--dry-run",
            "--no-acl"
        ];

        var options = ProvisionOptions.Parse(args);

        Assert.Equal(@"C:\dlm\school.dlmpack", options.PackPath);
        Assert.True(options.Quiet);
        Assert.True(options.PassphraseStdin);
        Assert.Equal(@"C:\ProgramData\DELIMa", options.TargetDirectory);
        Assert.Equal(@"\\share\lab_checklist.csv", options.ChecklistPath);
        Assert.Equal("Pelajar", options.PupilAccount);
        Assert.True(options.DryRun);
        Assert.False(options.ApplyAcls);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void ProvisionOptions_Parsing_HandlesShortFlagsAndHelp()
    {
        string[] args = ["-p", @"E:\school.dlmpack", "-q", "-t", @"C:\Data", "-c", @"E:\checklist.csv", "-a", "MuridLab"];
        var options = ProvisionOptions.Parse(args);

        Assert.Equal(@"E:\school.dlmpack", options.PackPath);
        Assert.True(options.Quiet);
        Assert.Equal(@"C:\Data", options.TargetDirectory);
        Assert.Equal(@"E:\checklist.csv", options.ChecklistPath);
        Assert.Equal("MuridLab", options.PupilAccount);
        Assert.True(options.ApplyAcls);

        var helpOptions = ProvisionOptions.Parse(["-h"]);
        Assert.True(helpOptions.ShowHelp);
    }
}
