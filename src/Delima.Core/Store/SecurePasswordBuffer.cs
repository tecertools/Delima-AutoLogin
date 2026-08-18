using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Delima.Core.Store;

/// <summary>
/// Holds sensitive password characters in a pinned memory buffer.
/// Wipes backing memory immediately with CryptographicOperations.ZeroMemory upon disposal.
/// </summary>
public sealed class SecurePasswordBuffer : ICredential
{
    private readonly char[] _buffer;
    private readonly int _length;
    private GCHandle _handle;
    private bool _disposed;

    public SecurePasswordBuffer(ReadOnlySpan<char> source)
    {
        _length = source.Length;
        _buffer = new char[_length];
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        source.CopyTo(_buffer);
    }

    public SecurePasswordBuffer(ReadOnlySpan<byte> utf8Bytes)
    {
        int charCount = System.Text.Encoding.UTF8.GetCharCount(utf8Bytes);
        _length = charCount;
        _buffer = new char[charCount];
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        System.Text.Encoding.UTF8.GetChars(utf8Bytes, _buffer);
    }

    public ReadOnlySpan<char> PasswordSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffer.AsSpan(0, _length);
        }
    }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Verification helper for testing. Checks if all characters in the backing buffer are zeroed.
    /// </summary>
    public bool IsBackingBufferZeroed()
    {
        foreach (char c in _buffer)
        {
            if (c != '\0') return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_buffer.AsSpan()));
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~SecurePasswordBuffer()
    {
        Dispose();
    }
}
