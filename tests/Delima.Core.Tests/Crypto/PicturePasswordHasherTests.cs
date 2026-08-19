using Delima.Core.Crypto;
using Delima.Core.Store;

namespace Delima.Core.Tests.Crypto;

public class PicturePasswordHasherTests
{
    [Fact]
    public void CreatePicturePassword_ValidSequence_GeneratesArgon2idHash()
    {
        var sequence = new[] { "bola", "kucing", "kereta" };
        var info = PicturePasswordHasher.CreatePicturePassword(sequence, Argon2Parameters.FastTest);

        Assert.NotNull(info);
        Assert.Equal("argon2id", info.Algo);
        Assert.False(string.IsNullOrWhiteSpace(info.Salt));
        Assert.False(string.IsNullOrWhiteSpace(info.Hash));

        byte[] salt = Convert.FromBase64String(info.Salt);
        byte[] hash = Convert.FromBase64String(info.Hash);

        Assert.Equal(PicturePasswordHasher.DefaultSaltSizeBytes, salt.Length);
        Assert.Equal(PicturePasswordHasher.HashSizeBytes, hash.Length);
    }

    [Fact]
    public void VerifyPicturePassword_CorrectSequence_ReturnsTrue()
    {
        var sequence = new[] { "ikan", "bunga", "bintang" };
        var info = PicturePasswordHasher.CreatePicturePassword(sequence, Argon2Parameters.FastTest);

        bool isValid = PicturePasswordHasher.VerifyPicturePassword(sequence, info, Argon2Parameters.FastTest);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPicturePassword_IncorrectSequence_ReturnsFalse()
    {
        var correctSequence = new[] { "ikan", "bunga", "bintang" };
        var wrongSequence = new[] { "ikan", "bunga", "epal" };
        var info = PicturePasswordHasher.CreatePicturePassword(correctSequence, Argon2Parameters.FastTest);

        bool isValid = PicturePasswordHasher.VerifyPicturePassword(wrongSequence, info, Argon2Parameters.FastTest);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifyPicturePassword_SameIconsDifferentOrder_ReturnsFalse()
    {
        var sequence1 = new[] { "bola", "kucing", "kereta" };
        var sequence2 = new[] { "kucing", "bola", "kereta" };
        var info = PicturePasswordHasher.CreatePicturePassword(sequence1, Argon2Parameters.FastTest);

        bool isValid = PicturePasswordHasher.VerifyPicturePassword(sequence2, info, Argon2Parameters.FastTest);

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void CreatePicturePassword_InvalidIconCount_ThrowsArgumentException(int count)
    {
        var sequence = Enumerable.Range(0, count).Select(i => $"icon_{i}").ToList();

        Assert.Throws<ArgumentException>(() => PicturePasswordHasher.CreatePicturePassword(sequence, Argon2Parameters.FastTest));
    }

    [Fact]
    public void CreatePicturePassword_NullOrEmptyIcon_ThrowsArgumentException()
    {
        var sequence = new[] { "bola", "", "kereta" };

        Assert.Throws<ArgumentException>(() => PicturePasswordHasher.CreatePicturePassword(sequence, Argon2Parameters.FastTest));
    }

    [Fact]
    public void VerifyPicturePassword_TamperedHash_ReturnsFalse()
    {
        var sequence = new[] { "bola", "kucing", "kereta" };
        var info = PicturePasswordHasher.CreatePicturePassword(sequence, Argon2Parameters.FastTest);

        byte[] rawHash = Convert.FromBase64String(info.Hash);
        rawHash[0] ^= 0xFF; // Flip bits
        info.Hash = Convert.ToBase64String(rawHash);

        bool isValid = PicturePasswordHasher.VerifyPicturePassword(sequence, info, Argon2Parameters.FastTest);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifyPicturePassword_TamperedSalt_ReturnsFalse()
    {
        var sequence = new[] { "bola", "kucing", "kereta" };
        var info = PicturePasswordHasher.CreatePicturePassword(sequence, Argon2Parameters.FastTest);

        byte[] rawSalt = Convert.FromBase64String(info.Salt);
        rawSalt[0] ^= 0xFF; // Flip bits
        info.Salt = Convert.ToBase64String(rawSalt);

        bool isValid = PicturePasswordHasher.VerifyPicturePassword(sequence, info, Argon2Parameters.FastTest);

        Assert.False(isValid);
    }

    [Fact]
    public void VerifyPicturePassword_NullOrInvalidInfo_ReturnsFalse()
    {
        var sequence = new[] { "bola", "kucing", "kereta" };

        Assert.False(PicturePasswordHasher.VerifyPicturePassword(sequence, null, Argon2Parameters.FastTest));
        Assert.False(PicturePasswordHasher.VerifyPicturePassword(sequence, new PicturePasswordInfo { Algo = "sha256", Salt = "abc", Hash = "def" }, Argon2Parameters.FastTest));
    }
}
