using System.Buffers.Binary;

namespace Delima.Core.Crypto;

/// <summary>
/// Represents the 67-byte binary header of a school.dlmpack bundle.
/// Serves as the Associated Data (AD) for AES-256-GCM authentication.
/// </summary>
public sealed class MasterBundleHeader
{
    public const int HeaderSizeBytes = 67;
    public const int SaltSizeBytes = 32;
    public const int NonceSizeBytes = 12;
    public const int TagSizeBytes = 16;
    public const byte KdfArgon2id = 1;

    public static readonly byte[] Magic = "DLMPACK\0"u8.ToArray();

    public ushort SchemaVersion { get; init; } = 2;
    public byte KdfId { get; init; } = KdfArgon2id;
    public uint ArgonMemoryKiB { get; init; } = 65536;
    public uint ArgonIterations { get; init; } = 3;
    public uint ArgonParallelism { get; init; } = 4;
    public byte[] Salt { get; init; } = new byte[SaltSizeBytes];
    public byte[] Nonce { get; init; } = new byte[NonceSizeBytes];

    public byte[] ToBytes()
    {
        byte[] buffer = new byte[HeaderSizeBytes];
        WriteTo(buffer);
        return buffer;
    }

    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < HeaderSizeBytes)
            throw new ArgumentException($"Destination must be at least {HeaderSizeBytes} bytes.", nameof(destination));

        Magic.CopyTo(destination[0..8]);
        BinaryPrimitives.WriteUInt16BigEndian(destination[8..10], SchemaVersion);
        destination[10] = KdfId;
        BinaryPrimitives.WriteUInt32BigEndian(destination[11..15], ArgonMemoryKiB);
        BinaryPrimitives.WriteUInt32BigEndian(destination[15..19], ArgonIterations);
        BinaryPrimitives.WriteUInt32BigEndian(destination[19..23], ArgonParallelism);
        Salt.CopyTo(destination[23..55]);
        Nonce.CopyTo(destination[55..67]);
    }

    public static MasterBundleHeader ReadFrom(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderSizeBytes)
            throw new InvalidDataException($"Header too short: expected {HeaderSizeBytes} bytes, got {source.Length}.");

        if (!source[0..8].SequenceEqual(Magic))
            throw new InvalidDataException("Invalid magic bytes in master bundle header.");

        ushort schemaVer = BinaryPrimitives.ReadUInt16BigEndian(source[8..10]);
        byte kdfId = source[10];
        if (kdfId != KdfArgon2id)
            throw new InvalidDataException($"Unsupported KDF ID: {kdfId}.");

        uint argonM = BinaryPrimitives.ReadUInt32BigEndian(source[11..15]);
        uint argonT = BinaryPrimitives.ReadUInt32BigEndian(source[15..19]);
        uint argonP = BinaryPrimitives.ReadUInt32BigEndian(source[19..23]);

        if (argonP == 0 || argonP > 64 || argonT == 0 || argonT > 50 || argonM < 8 * argonP || argonM > 1024 * 1024)
        {
            throw new InvalidDataException("Invalid or unsafe Argon2 parameters in bundle header.");
        }

        byte[] salt = source[23..55].ToArray();
        byte[] nonce = source[55..67].ToArray();

        return new MasterBundleHeader
        {
            SchemaVersion = schemaVer,
            KdfId = kdfId,
            ArgonMemoryKiB = argonM,
            ArgonIterations = argonT,
            ArgonParallelism = argonP,
            Salt = salt,
            Nonce = nonce
        };
    }
}
