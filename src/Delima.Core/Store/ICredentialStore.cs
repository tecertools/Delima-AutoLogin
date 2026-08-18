namespace Delima.Core.Store;

/// <summary>
/// Abstraction for a credential store.
/// Implemented by the local DPAPI store on Windows or by master bundle readers during admin/provisioning.
/// </summary>
public interface ICredentialStore : IDisposable
{
    /// <summary>
    /// Opens the credential for a specific pupil, returning a disposable credential buffer.
    /// Never materialises the password as a string.
    /// </summary>
    ICredential OpenCredential(string studentId);

    /// <summary>
    /// Checks if a credential exists for the given pupil.
    /// </summary>
    bool HasCredential(string studentId);

    /// <summary>
    /// Schema version of the store.
    /// </summary>
    ushort SchemaVersion { get; }
}
