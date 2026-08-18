using System.Security.Cryptography;

namespace Delima.Core.Crypto;

/// <summary>
/// Helper for HMAC-SHA256 calculation and constant-time verification.
/// </summary>
public static class HmacHelper
{
    public const int HmacSizeBytes = 32;

    public static byte[] ComputeHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        using var hmac = new HMACSHA256(key.ToArray());
        return hmac.ComputeHash(data.ToArray());
    }

    public static bool VerifyHmac(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, ReadOnlySpan<byte> expectedHmac)
    {
        byte[] computed = ComputeHmac(key, data);
        try
        {
            return CryptographicOperations.FixedTimeEquals(computed, expectedHmac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computed);
        }
    }
}
