using System.Security.Cryptography;
using System.Text;
using Delima.Core.Store;

namespace Delima.Core.Crypto;

/// <summary>
/// Handles Argon2id hashing, generation, and constant-time verification for 3-icon picture passwords.
/// Adheres to Technical Architecture §3.2 and PRD §7.3.
/// </summary>
public static class PicturePasswordHasher
{
    public const int DefaultSaltSizeBytes = 16;
    public const int HashSizeBytes = 32; // 256-bit hash
    public const string DefaultAlgo = "argon2id";
    public const char SequenceDelimiter = ':';

    /// <summary>
    /// Hashes a picture password sequence (e.g. ["bola", "kucing", "kereta"]) into a PicturePasswordInfo object.
    /// </summary>
    public static PicturePasswordInfo CreatePicturePassword(
        IEnumerable<string> iconSequence,
        Argon2Parameters? parameters = null,
        byte[]? customSalt = null)
    {
        ArgumentNullException.ThrowIfNull(iconSequence);

        var sequenceList = iconSequence.ToList();
        if (sequenceList.Count != 3)
        {
            throw new ArgumentException("Picture password must contain exactly 3 icons.", nameof(iconSequence));
        }

        foreach (var icon in sequenceList)
        {
            if (string.IsNullOrWhiteSpace(icon))
            {
                throw new ArgumentException("Icon identifier cannot be empty or whitespace.", nameof(iconSequence));
            }
        }

        byte[] salt = new byte[DefaultSaltSizeBytes];
        if (customSalt != null)
        {
            if (customSalt.Length < 16)
            {
                throw new ArgumentException("Salt must be at least 16 bytes.", nameof(customSalt));
            }
            salt = (byte[])customSalt.Clone();
        }
        else
        {
            RandomNumberGenerator.Fill(salt);
        }

        Argon2Parameters kdfParams = parameters ?? Argon2Parameters.Default;
        string combined = string.Join(SequenceDelimiter, sequenceList);
        byte[] rawSequenceBytes = Encoding.UTF8.GetBytes(combined);

        byte[] derivedHash;
        try
        {
            derivedHash = Argon2Kdf.DeriveKey(rawSequenceBytes, salt, kdfParams, HashSizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rawSequenceBytes);
        }

        return new PicturePasswordInfo
        {
            Algo = DefaultAlgo,
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(derivedHash)
        };
    }

    /// <summary>
    /// Verifies an entered picture password sequence against stored PicturePasswordInfo in constant time.
    /// </summary>
    public static bool VerifyPicturePassword(
        IEnumerable<string> iconSequence,
        PicturePasswordInfo? info,
        Argon2Parameters? parameters = null)
    {
        if (info == null || string.IsNullOrWhiteSpace(info.Salt) || string.IsNullOrWhiteSpace(info.Hash))
        {
            return false;
        }

        if (!string.Equals(info.Algo, DefaultAlgo, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var sequenceList = iconSequence?.ToList();
        if (sequenceList == null || sequenceList.Count != 3)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(info.Salt);
            expectedHash = Convert.FromBase64String(info.Hash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length < 16 || expectedHash.Length != HashSizeBytes)
        {
            return false;
        }

        Argon2Parameters kdfParams = parameters ?? Argon2Parameters.Default;
        string combined = string.Join(SequenceDelimiter, sequenceList);
        byte[] rawSequenceBytes = Encoding.UTF8.GetBytes(combined);

        byte[] computedHash;
        try
        {
            computedHash = Argon2Kdf.DeriveKey(rawSequenceBytes, salt, kdfParams, HashSizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rawSequenceBytes);
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computedHash);
        }
    }
}
