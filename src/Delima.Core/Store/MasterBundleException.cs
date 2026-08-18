namespace Delima.Core.Store;

/// <summary>
/// Thrown when master bundle authentication, decryption, or decompression fails.
/// Deliberately generic to prevent leaking whether the passphrase, header, or ciphertext was the cause.
/// </summary>
public sealed class MasterBundleException : Exception
{
    public MasterBundleException(string message) : base(message) { }
    public MasterBundleException(string message, Exception innerException) : base(message, innerException) { }
}
