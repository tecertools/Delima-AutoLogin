using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Delima.Core.Crypto;

namespace Delima.Core.Store;

/// <summary>
/// Provides packing and unpacking for school.dlmpack files per Technical Architecture §3.2.
/// </summary>
public static class MasterBundle
{
    public static byte[] Pack(
        MasterBundlePayload payload,
        ReadOnlySpan<char> adminPassphrase,
        Argon2Parameters? argonParameters = null)
    {
        var kdfParams = argonParameters ?? Argon2Parameters.Default;

        // 1. Serialize payload to JSON and compress
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        byte[] uncompressedBytes = Encoding.UTF8.GetBytes(json);
        byte[] compressedPayload;
        using (var outputStream = new MemoryStream())
        {
            using (var deflate = new DeflateStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(uncompressedBytes);
            }
            compressedPayload = outputStream.ToArray();
        }

        // 2. Prepare header with CSPRNG salt and nonce
        byte[] salt = new byte[MasterBundleHeader.SaltSizeBytes];
        byte[] nonce = new byte[MasterBundleHeader.NonceSizeBytes];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        var header = new MasterBundleHeader
        {
            SchemaVersion = payload.SchemaVersion,
            KdfId = MasterBundleHeader.KdfArgon2id,
            ArgonMemoryKiB = kdfParams.MemorySizeKiB,
            ArgonIterations = kdfParams.Iterations,
            ArgonParallelism = kdfParams.DegreeOfParallelism,
            Salt = salt,
            Nonce = nonce
        };

        byte[] headerBytes = header.ToBytes();

        // 3. Derive 256-bit key
        byte[] key = Argon2Kdf.DeriveKey(adminPassphrase, salt, kdfParams);

        // 4. Encrypt with AES-256-GCM using headerBytes as Associated Data
        byte[] ciphertext = new byte[compressedPayload.Length];
        byte[] tag = new byte[MasterBundleHeader.TagSizeBytes];

        try
        {
            using var aesGcm = new AesGcm(key, MasterBundleHeader.TagSizeBytes);
            aesGcm.Encrypt(
                nonce: nonce,
                plaintext: compressedPayload,
                ciphertext: ciphertext,
                tag: tag,
                associatedData: headerBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(uncompressedBytes);
            CryptographicOperations.ZeroMemory(compressedPayload);
        }

        // 5. Construct full bundle: [Header (67)] [Ciphertext (N)] [Tag (16)]
        byte[] bundle = new byte[MasterBundleHeader.HeaderSizeBytes + ciphertext.Length + MasterBundleHeader.TagSizeBytes];
        Buffer.BlockCopy(headerBytes, 0, bundle, 0, MasterBundleHeader.HeaderSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, bundle, MasterBundleHeader.HeaderSizeBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, bundle, MasterBundleHeader.HeaderSizeBytes + ciphertext.Length, MasterBundleHeader.TagSizeBytes);

        return bundle;
    }

    public static MasterBundlePayload Unpack(
        ReadOnlySpan<byte> bundleBytes,
        ReadOnlySpan<char> adminPassphrase)
    {
        if (bundleBytes.Length < MasterBundleHeader.HeaderSizeBytes + MasterBundleHeader.TagSizeBytes)
        {
            throw new MasterBundleException("Authentication failed: bundle is too short or corrupted.");
        }

        try
        {
            MasterBundleHeader header = MasterBundleHeader.ReadFrom(bundleBytes[0..MasterBundleHeader.HeaderSizeBytes]);
            byte[] headerBytes = bundleBytes[0..MasterBundleHeader.HeaderSizeBytes].ToArray();
            int ciphertextLength = bundleBytes.Length - MasterBundleHeader.HeaderSizeBytes - MasterBundleHeader.TagSizeBytes;
            ReadOnlySpan<byte> ciphertext = bundleBytes.Slice(MasterBundleHeader.HeaderSizeBytes, ciphertextLength);
            ReadOnlySpan<byte> tag = bundleBytes.Slice(bundleBytes.Length - MasterBundleHeader.TagSizeBytes, MasterBundleHeader.TagSizeBytes);

            var kdfParams = new Argon2Parameters(header.ArgonMemoryKiB, header.ArgonIterations, header.ArgonParallelism);
            byte[] key = Argon2Kdf.DeriveKey(adminPassphrase, header.Salt, kdfParams);
            byte[] plaintext = new byte[ciphertextLength];

            try
            {
                using var aesGcm = new AesGcm(key, MasterBundleHeader.TagSizeBytes);
                aesGcm.Decrypt(
                    nonce: header.Nonce,
                    ciphertext: ciphertext,
                    tag: tag,
                    plaintext: plaintext,
                    associatedData: headerBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }

            // Decompress and deserialize JSON
            byte[] decompressedBytes;
            using (var inputStream = new MemoryStream(plaintext))
            using (var deflate = new DeflateStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                deflate.CopyTo(outputStream);
                decompressedBytes = outputStream.ToArray();
            }

            try
            {
                string json = Encoding.UTF8.GetString(decompressedBytes);
                var payload = JsonSerializer.Deserialize<MasterBundlePayload>(json);
                if (payload == null)
                {
                    throw new MasterBundleException("Authentication failed: corrupted payload content.");
                }
                return payload;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(decompressedBytes);
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch (Exception ex) when (ex is not MasterBundleException)
        {
            throw new MasterBundleException("Authentication failed: invalid passphrase or corrupted bundle.", ex);
        }
    }
}
