using Delima.Core.Crypto;
using Delima.Core.Store;
using Xunit;

namespace Delima.Core.Tests.Store;

public class MasterBundleTests
{
    private static MasterBundlePayload CreateSyntheticPayload()
    {
        return new MasterBundlePayload
        {
            SchemaVersion = 2,
            School = new SchoolInfo
            {
                Code = "TEST01",
                Name = "Sekolah Ujian Kebangsaan",
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
                    new DestinationConfig { Id = "delima", Label = "DELIMa 3.0", Url = "https://d3.delima.edu.my/" },
                    new DestinationConfig { Id = "classroom", Label = "Google Classroom", Url = "https://classroom.google.com/" }
                ],
                PicturePasswordRequired = true,
                IdleResetSeconds = 600,
                InjectionSettleMs = 400,
                WindowWaitTimeoutMs = 30000,
                StoreMaxAgeDays = 30
            },
            Classes = [
                new ClassInfo { Id = "c_1", Name = "1 Cemerlang", Grade = 1, ColourIndex = 0 },
                new ClassInfo { Id = "c_2", Name = "2 Amanah", Grade = 2, ColourIndex = 1 }
            ],
            Students = [
                new StudentInfo
                {
                    Id = "s_0001",
                    Name = "Murid Ujian Satu",
                    ClassId = "c_1",
                    EmailLocal = "m-10000001",
                    Avatar = "kucing",
                    Password = "SyntheticTestPassword1!",
                    PasswordVersion = 1,
                    PicturePassword = new PicturePasswordInfo
                    {
                        Algo = "argon2id",
                        Salt = "synthetic-salt",
                        Hash = "synthetic-hash"
                    }
                }
            ]
        };
    }

    [Fact]
    public void MasterBundle_FullRoundTrip_Succeeds()
    {
        var payload = CreateSyntheticPayload();
        string adminPassphrase = "CorrectAdminPassphrase123#";

        byte[] bundle = MasterBundle.Pack(payload, adminPassphrase.AsSpan(), Argon2Parameters.FastTest);

        Assert.NotNull(bundle);
        Assert.True(bundle.Length > MasterBundleHeader.HeaderSizeBytes + MasterBundleHeader.TagSizeBytes);

        MasterBundlePayload restored = MasterBundle.Unpack(bundle, adminPassphrase.AsSpan());

        Assert.NotNull(restored);
        Assert.Equal(payload.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(payload.School.Code, restored.School.Code);
        Assert.Equal(payload.School.Name, restored.School.Name);
        Assert.Equal(payload.School.Domain, restored.School.Domain);
        Assert.Equal(payload.Theme.Primary, restored.Theme.Primary);
        Assert.Equal(payload.Classes.Count, restored.Classes.Count);
        Assert.Equal(payload.Students.Count, restored.Students.Count);
        Assert.Equal(payload.Students[0].Id, restored.Students[0].Id);
        Assert.Equal(payload.Students[0].Name, restored.Students[0].Name);
        Assert.Equal(payload.Students[0].Password, restored.Students[0].Password);
        Assert.Equal(payload.Config.Destinations.Count, restored.Config.Destinations.Count);
    }

    [Fact]
    public void MasterBundle_DefaultProductionArgon2Parameters_RoundTripSucceeds()
    {
        var payload = CreateSyntheticPayload();
        string adminPassphrase = "ProductionAdminPassphrase123!";

        // Test with default production Argon2 parameters (64 MiB, 3 iterations, 4 lanes)
        byte[] bundle = MasterBundle.Pack(payload, adminPassphrase.AsSpan(), Argon2Parameters.Default);
        MasterBundlePayload restored = MasterBundle.Unpack(bundle, adminPassphrase.AsSpan());

        Assert.Equal(payload.School.Code, restored.School.Code);
        Assert.Equal(payload.Students[0].Password, restored.Students[0].Password);
    }

    [Fact]
    public void MasterBundle_WrongPassphrase_FailsWithoutLeakingReason()
    {
        var payload = CreateSyntheticPayload();
        string correctPassphrase = "CorrectAdminPassphrase123#";
        string wrongPassphrase = "WrongAdminPassphrase456!";

        byte[] bundle = MasterBundle.Pack(payload, correctPassphrase.AsSpan(), Argon2Parameters.FastTest);

        var ex = Assert.Throws<MasterBundleException>(() => MasterBundle.Unpack(bundle, wrongPassphrase.AsSpan()));
        Assert.Contains("Authentication failed", ex.Message);
    }

    [Theory]
    [InlineData(0)]  // Flip magic byte
    [InlineData(8)]  // Flip schema_ver byte
    [InlineData(10)] // Flip kdf_id byte
    [InlineData(12)] // Flip argon_m memory parameter byte
    [InlineData(16)] // Flip argon_t iteration parameter byte
    [InlineData(20)] // Flip argon_p parallelism parameter byte
    [InlineData(30)] // Flip salt byte
    [InlineData(60)] // Flip nonce byte
    public void MasterBundle_TamperTest_HeaderModification_FailsDecryption(int byteOffset)
    {
        // PROVES: Modifying any byte in the 67-byte header (including KDF parameters, salt, or nonce)
        // fails AES-256-GCM authentication because the entire header is included as Associated Data.
        var payload = CreateSyntheticPayload();
        string adminPassphrase = "AdminPassphraseForHeaderTamper!";
        byte[] bundle = MasterBundle.Pack(payload, adminPassphrase.AsSpan(), Argon2Parameters.FastTest);

        // Flip one byte in the header
        byte[] tamperedBundle = (byte[])bundle.Clone();
        tamperedBundle[byteOffset] ^= 0xFF;

        var ex = Assert.Throws<MasterBundleException>(() => MasterBundle.Unpack(tamperedBundle, adminPassphrase.AsSpan()));
        Assert.Contains("Authentication failed", ex.Message);
    }

    [Fact]
    public void MasterBundle_TamperTest_CiphertextModification_FailsDecryption()
    {
        // PROVES: Modifying any byte in the ciphertext payload fails AES-256-GCM authentication.
        // The ciphertext cannot be modified or spliced without causing an authentication tag mismatch.
        var payload = CreateSyntheticPayload();
        string adminPassphrase = "AdminPassphraseForCiphertextTamper!";
        byte[] bundle = MasterBundle.Pack(payload, adminPassphrase.AsSpan(), Argon2Parameters.FastTest);

        // Ciphertext starts right after header (offset 67)
        int ciphertextOffset = MasterBundleHeader.HeaderSizeBytes + 5;
        byte[] tamperedBundle = (byte[])bundle.Clone();
        tamperedBundle[ciphertextOffset] ^= 0xFF;

        var ex = Assert.Throws<MasterBundleException>(() => MasterBundle.Unpack(tamperedBundle, adminPassphrase.AsSpan()));
        Assert.Contains("Authentication failed", ex.Message);
    }

    [Theory]
    [InlineData(1)]  // Last byte of tag
    [InlineData(8)]  // Middle byte of tag
    [InlineData(16)] // First byte of tag
    public void MasterBundle_TamperTest_TagModification_FailsDecryption(int bytesFromEnd)
    {
        // PROVES: Modifying any byte in the 16-byte authentication tag causes AES-256-GCM authentication failure.
        // An attacker cannot forge or tamper with the authentication tag.
        var payload = CreateSyntheticPayload();
        string adminPassphrase = "AdminPassphraseForTagTamper!";
        byte[] bundle = MasterBundle.Pack(payload, adminPassphrase.AsSpan(), Argon2Parameters.FastTest);

        int tagOffset = bundle.Length - bytesFromEnd;
        byte[] tamperedBundle = (byte[])bundle.Clone();
        tamperedBundle[tagOffset] ^= 0xFF;

        var ex = Assert.Throws<MasterBundleException>(() => MasterBundle.Unpack(tamperedBundle, adminPassphrase.AsSpan()));
        Assert.Contains("Authentication failed", ex.Message);
    }

    [Fact]
    public void MasterBundle_TooShortBundle_FailsDecryption()
    {
        byte[] shortBundle = new byte[50];
        var ex = Assert.Throws<MasterBundleException>(() => MasterBundle.Unpack(shortBundle, "passphrase".AsSpan()));
        Assert.Contains("Authentication failed", ex.Message);
    }
}
