using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Delima.Core.Crypto;

/// <summary>
/// Parameters for Argon2id key derivation.
/// </summary>
public sealed record Argon2Parameters(
    uint MemorySizeKiB = 65536, // 64 MiB
    uint Iterations = 3,
    uint DegreeOfParallelism = 4
)
{
    public static readonly Argon2Parameters Default = new(65536, 3, 4);
    public static readonly Argon2Parameters FastTest = new(1024, 1, 1);
}

/// <summary>
/// Wraps Konscious.Security.Cryptography.Argon2id for deriving AES-256 keys.
/// </summary>
public static class Argon2Kdf
{
    public const int KeyLengthBytes = 32; // 256-bit key for AES-256

    public static byte[] DeriveKey(
        ReadOnlySpan<char> passphrase,
        ReadOnlySpan<byte> salt,
        Argon2Parameters parameters,
        int keyLength = KeyLengthBytes)
    {
        byte[] passphraseBytes = Encoding.UTF8.GetBytes(passphrase.ToArray());
        try
        {
            return DeriveKey(passphraseBytes, salt, parameters, keyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passphraseBytes);
        }
    }

    public static byte[] DeriveKey(
        ReadOnlySpan<byte> passphraseBytes,
        ReadOnlySpan<byte> salt,
        Argon2Parameters parameters,
        int keyLength = KeyLengthBytes)
    {
        if (parameters.DegreeOfParallelism == 0 || parameters.DegreeOfParallelism > 64 ||
            parameters.Iterations == 0 || parameters.Iterations > 50 ||
            parameters.MemorySizeKiB < 8 * parameters.DegreeOfParallelism ||
            parameters.MemorySizeKiB > 1024 * 1024)
        {
            throw new ArgumentException("Invalid or unsafe Argon2 parameters.");
        }

        using var argon2 = new Argon2id(passphraseBytes.ToArray())
        {
            Salt = salt.ToArray(),
            MemorySize = (int)parameters.MemorySizeKiB,
            Iterations = (int)parameters.Iterations,
            DegreeOfParallelism = (int)parameters.DegreeOfParallelism
        };

        return argon2.GetBytes(keyLength);
    }
}
