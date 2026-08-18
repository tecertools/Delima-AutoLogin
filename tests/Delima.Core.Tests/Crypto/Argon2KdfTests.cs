using System.Text;
using Delima.Core.Crypto;
using Xunit;

namespace Delima.Core.Tests.Crypto;

public class Argon2KdfTests
{
    [Fact]
    public void DeriveKey_DeterministicOutput_ForIdenticalInputs()
    {
        string passphrase = "test-passphrase-admin-12345";
        byte[] salt = Encoding.UTF8.GetBytes("01234567890123456789012345678901"); // 32 bytes
        var parameters = Argon2Parameters.FastTest;

        byte[] key1 = Argon2Kdf.DeriveKey(passphrase.AsSpan(), salt, parameters);
        byte[] key2 = Argon2Kdf.DeriveKey(passphrase.AsSpan(), salt, parameters);

        Assert.Equal(Argon2Kdf.KeyLengthBytes, key1.Length);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void DeriveKey_DifferentInputs_ProduceDifferentOutputs()
    {
        byte[] salt1 = Encoding.UTF8.GetBytes("01234567890123456789012345678901");
        byte[] salt2 = Encoding.UTF8.GetBytes("01234567890123456789012345678902");
        var parameters = Argon2Parameters.FastTest;

        byte[] keyPass1 = Argon2Kdf.DeriveKey("passphrase-A".AsSpan(), salt1, parameters);
        byte[] keyPass2 = Argon2Kdf.DeriveKey("passphrase-B".AsSpan(), salt1, parameters);
        byte[] keySalt2 = Argon2Kdf.DeriveKey("passphrase-A".AsSpan(), salt2, parameters);

        Assert.NotEqual(keyPass1, keyPass2);
        Assert.NotEqual(keyPass1, keySalt2);
    }
}
