namespace Delima.Core.Store;

/// <summary>
/// Represents a decrypted credential that holds a sensitive password in memory.
/// The backing memory is pinned and wiped immediately upon disposal.
/// </summary>
public interface ICredential : IDisposable
{
    /// <summary>
    /// Gets the password characters as a read-only span.
    /// Never convert this to a System.String.
    /// </summary>
    ReadOnlySpan<char> PasswordSpan { get; }
}
