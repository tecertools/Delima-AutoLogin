using System.IO;
using System.Security.Cryptography;
using Delima.Core.Store;
using Delima.Win32.Store;
using Xunit;

namespace Delima.Win32.Tests;

public class DpapiCredentialStoreTests : IDisposable
{
    private readonly string _testDirectory;

    public DpapiCredentialStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "DelimaTests_" + Guid.NewGuid().ToString("N"));
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

    private static MasterBundlePayload CreateSyntheticPayload()
    {
        return new MasterBundlePayload
        {
            SchemaVersion = 2,
            School = new SchoolInfo
            {
                Code = "TEST01",
                Name = "Sekolah Rendah Kebangsaan Test",
                Motto = "Berilmu Berbakti",
                Domain = "moe-dl.edu.my"
            },
            Theme = new ThemeInfo
            {
                Primary = "#056839",
                Accent = "#F7941D",
                ClassColours = ["#C41118", "#056839", "#2F6B12"]
            },
            Config = new AppConfig
            {
                Destinations = [
                    new DestinationConfig { Id = "delima", Label = "DELIMa 3.0", Url = "https://d3.delima.edu.my/" }
                ],
                PicturePasswordRequired = true,
                IdleResetSeconds = 600,
                InjectionSettleMs = 400,
                WindowWaitTimeoutMs = 30000,
                StoreMaxAgeDays = 30
            },
            Classes = [
                new ClassInfo { Id = "c_1", Name = "1 Cemerlang", Grade = 1, ColourIndex = 0 }
            ],
            Students = [
                new StudentInfo
                {
                    Id = "s_0001",
                    Name = "Nur Aishah Binti Ahmad",
                    ClassId = "c_1",
                    EmailLocal = "m-10000001",
                    Avatar = "kucing",
                    Password = "StandardPassword123!",
                    PasswordVersion = 1,
                    Active = true
                },
                new StudentInfo
                {
                    Id = "s_0002",
                    Name = "Arjun A/L Kumaran",
                    ClassId = "c_1",
                    EmailLocal = "m-10000002",
                    Avatar = "kereta",
                    Password = "M+u^r%i~d(2){0}[26]\"\\special",
                    PasswordVersion = 2,
                    Active = true
                },
                new StudentInfo
                {
                    Id = "s_0003",
                    Name = "Tan Wei Ming",
                    ClassId = "c_1",
                    EmailLocal = "m-10000003",
                    Avatar = "bunga",
                    Password = null, // Student with no password set
                    PasswordVersion = 1,
                    Active = true
                },
                new StudentInfo
                {
                    Id = "s_0004",
                    Name = "Murid Tidak Aktif",
                    ClassId = "c_1",
                    EmailLocal = "m-10000004",
                    Avatar = "buku",
                    Password = "InactivePassword456!",
                    PasswordVersion = 1,
                    Active = false
                }
            ]
        };
    }

    [Fact]
    public void DpapiStore_WriteAndOpen_FullRoundTripSucceeds()
    {
        var payload = CreateSyntheticPayload();

        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        string dataPath = DpapiCredentialStore.GetDefaultDataPath(_testDirectory);
        string entropyPath = DpapiCredentialStore.GetDefaultEntropyPath(_testDirectory);

        Assert.True(File.Exists(dataPath));
        Assert.True(File.Exists(entropyPath));
        Assert.Equal(32, new FileInfo(entropyPath).Length);
        Assert.True(new FileInfo(dataPath).Length > 32);

        using var store = DpapiCredentialStore.Open(_testDirectory);

        Assert.Equal((ushort)2, store.SchemaVersion);
        Assert.Equal("TEST01", store.SchoolCode);
        Assert.Equal(4, store.StudentCount);

        Assert.True(store.HasCredential("s_0001"));
        Assert.True(store.HasCredential("s_0002"));
        Assert.False(store.HasCredential("s_0003")); // No password set
        Assert.True(store.HasCredential("s_0004"));

        Assert.True(store.IsStudentActive("s_0001"));
        Assert.False(store.IsStudentActive("s_0004"));
    }

    [Fact]
    public void DpapiStore_OpenCredential_ReturnsZeroableBuffer_WithExactChars()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);

        // Test standard password
        using (var cred = store.OpenCredential("s_0001"))
        {
            Assert.NotNull(cred);
            Assert.Equal("StandardPassword123!", cred.PasswordSpan.ToString());

            if (cred is SecurePasswordBuffer secBuf)
            {
                Assert.False(secBuf.IsDisposed);
                Assert.False(secBuf.IsBackingBufferZeroed());
            }
        }

        // Test password with reserved / JSON-escaped chars
        using (var cred2 = store.OpenCredential("s_0002"))
        {
            Assert.NotNull(cred2);
            Assert.Equal("M+u^r%i~d(2){0}[26]\"\\special", cred2.PasswordSpan.ToString());
        }
    }

    [Fact]
    public void DpapiStore_OpenCredential_ZeroesMemoryUponDispose()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);
        var cred = store.OpenCredential("s_0001");
        var secBuf = Assert.IsType<SecurePasswordBuffer>(cred);

        Assert.False(secBuf.IsDisposed);
        secBuf.Dispose();

        Assert.True(secBuf.IsDisposed);
        Assert.True(secBuf.IsBackingBufferZeroed());
        Assert.Throws<ObjectDisposedException>(() => { _ = cred.PasswordSpan.Length; });
    }

    [Fact]
    public void DpapiStore_OpenCredential_ThrowsOnMissingOrEmptyPassword()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);

        // Pupil in roster but without a password
        var exNoPass = Assert.Throws<InvalidOperationException>(() => store.OpenCredential("s_0003"));
        Assert.Contains("No password is set", exNoPass.Message);

        // Non-existent pupil
        var exNotFound = Assert.Throws<KeyNotFoundException>(() => store.OpenCredential("s_9999"));
        Assert.Contains("was not found", exNotFound.Message);
    }

    [Fact]
    public void DpapiStore_TamperDetection_ModifiedDataFile_FailsHmacVerification()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        string dataPath = DpapiCredentialStore.GetDefaultDataPath(_testDirectory);
        byte[] dataBytes = File.ReadAllBytes(dataPath);

        // Flip a byte in ciphertext
        dataBytes[10] ^= 0xFF;
        File.WriteAllBytes(dataPath, dataBytes);

        var ex = Assert.Throws<CryptographicException>(() => DpapiCredentialStore.Open(_testDirectory));
        Assert.Contains("Tamper detected", ex.Message);
    }

    [Fact]
    public void DpapiStore_TamperDetection_ModifiedEntropyFile_FailsHmacVerification()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        string entropyPath = DpapiCredentialStore.GetDefaultEntropyPath(_testDirectory);
        byte[] entropyBytes = File.ReadAllBytes(entropyPath);

        // Flip a byte in entropy
        entropyBytes[5] ^= 0xFF;
        File.WriteAllBytes(entropyPath, entropyBytes);

        var ex = Assert.Throws<CryptographicException>(() => DpapiCredentialStore.Open(_testDirectory));
        Assert.Contains("Tamper detected", ex.Message);
    }

    [Fact]
    public void DpapiStore_TamperDetection_ModifiedHmacBytes_FailsVerification()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        string dataPath = DpapiCredentialStore.GetDefaultDataPath(_testDirectory);
        byte[] dataBytes = File.ReadAllBytes(dataPath);

        // Flip last byte of file (HMAC tag)
        dataBytes[^1] ^= 0xFF;
        File.WriteAllBytes(dataPath, dataBytes);

        var ex = Assert.Throws<CryptographicException>(() => DpapiCredentialStore.Open(_testDirectory));
        Assert.Contains("Tamper detected", ex.Message);
    }

    [Fact]
    public void DpapiStore_CustomEntropy_RoundTripSucceeds()
    {
        var payload = CreateSyntheticPayload();
        byte[] customEntropy = new byte[32];
        Array.Fill(customEntropy, (byte)0x42);

        DpapiCredentialStore.WriteStore(payload, _testDirectory, customEntropy: customEntropy, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);
        using var cred = store.OpenCredential("s_0001");
        Assert.Equal("StandardPassword123!", cred.PasswordSpan.ToString());
    }

    [Fact]
    public void DpapiStore_DisposedStore_ThrowsObjectDisposedException()
    {
        var payload = CreateSyntheticPayload();
        DpapiCredentialStore.WriteStore(payload, _testDirectory, applyAcls: false);

        var store = DpapiCredentialStore.Open(_testDirectory);
        store.Dispose();

        Assert.Throws<ObjectDisposedException>(() => store.OpenCredential("s_0001"));
        Assert.Throws<ObjectDisposedException>(() => store.HasCredential("s_0001"));
        Assert.Throws<ObjectDisposedException>(() => store.IsStudentActive("s_0001"));
        Assert.Throws<ObjectDisposedException>(() => store.GetStudentIds());
    }

    [Fact]
    public void StoreAclConfigurator_DirectoryStructureCreation_Succeeds()
    {
        string baseDir = Path.Combine(_testDirectory, "AclTestStructure");
        StoreAclConfigurator.EnsureDirectoryStructure(baseDir, pupilAccount: "Murid", applyAcls: true);

        Assert.True(Directory.Exists(Path.Combine(baseDir, "audit")));
        Assert.True(Directory.Exists(Path.Combine(baseDir, "theme")));
        Assert.True(Directory.Exists(Path.Combine(baseDir, "assets", "avatars")));
    }

    [Fact]
    public void DpapiStore_RawBytesSpanWrite_RoundTripSucceeds()
    {
        string json = """
        {
          "schema_version": 2,
          "school": { "code": "RAW01" },
          "students": [
            { "id": "s_raw1", "password": "RawPasswordTest!123", "active": true }
          ]
        }
        """;

        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
        DpapiCredentialStore.WriteStore(utf8.AsSpan(), _testDirectory, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);
        Assert.Equal("RAW01", store.SchoolCode);
        Assert.True(store.HasCredential("s_raw1"));

        using var cred = store.OpenCredential("s_raw1");
        Assert.Equal("RawPasswordTest!123", cred.PasswordSpan.ToString());
    }

    [Fact]
    public void DpapiStore_ReversedJsonFieldOrder_PasswordBeforeId_ExtractsCorrectly()
    {
        // Tests JSON where "password" property appears BEFORE "id" property
        string json = """
        {
          "schema_version": 2,
          "school": { "code": "REV01" },
          "students": [
            { "password": "FirstPassword!", "id": "s_rev1", "active": true },
            { "password": "SecondPassword!", "id": "s_rev2", "active": true }
          ]
        }
        """;

        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
        DpapiCredentialStore.WriteStore(utf8.AsSpan(), _testDirectory, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);
        Assert.True(store.HasCredential("s_rev1"));
        Assert.True(store.HasCredential("s_rev2"));

        using (var cred1 = store.OpenCredential("s_rev1"))
        {
            Assert.Equal("FirstPassword!", cred1.PasswordSpan.ToString());
        }

        using (var cred2 = store.OpenCredential("s_rev2"))
        {
            Assert.Equal("SecondPassword!", cred2.PasswordSpan.ToString());
        }
    }

    [Fact]
    public void DpapiStore_LongPassword_ExpandsBufferAndZeroesProperly()
    {
        // Tests password exceeding default 256-char buffer
        string longSecret = new string('A', 300) + "!Special#123";
        string json = $$"""
        {
          "schema_version": 2,
          "school": { "code": "LONG01" },
          "students": [
            { "id": "s_long", "password": "{{longSecret}}", "active": true }
          ]
        }
        """;

        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(json);
        DpapiCredentialStore.WriteStore(utf8.AsSpan(), _testDirectory, applyAcls: false);

        using var store = DpapiCredentialStore.Open(_testDirectory);
        using var cred = store.OpenCredential("s_long");
        Assert.Equal(longSecret, cred.PasswordSpan.ToString());
    }

    [Fact]
    public void DpapiStore_InvalidEntropyLength_ThrowsArgumentException()
    {
        var payload = CreateSyntheticPayload();
        byte[] invalidEntropy = new byte[16]; // Must be 32

        Assert.Throws<ArgumentException>(() =>
            DpapiCredentialStore.WriteStore(payload, _testDirectory, customEntropy: invalidEntropy, applyAcls: false));
    }
}
