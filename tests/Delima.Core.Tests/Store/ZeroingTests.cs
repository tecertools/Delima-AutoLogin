using Delima.Core.Store;
using Xunit;

namespace Delima.Core.Tests.Store;

public class ZeroingTests
{
    [Fact]
    public void SecurePasswordBuffer_HoldsData_AndZeroesBackingMemoryOnDispose()
    {
        string secret = "SyntheticSecretPassword123!";
        var buffer = new SecurePasswordBuffer(secret.AsSpan());

        Assert.False(buffer.IsDisposed);
        Assert.Equal(secret, buffer.PasswordSpan.ToString());

        // Dispose should zero out memory
        buffer.Dispose();

        Assert.True(buffer.IsDisposed);
        Assert.True(buffer.IsBackingBufferZeroed());
        Assert.Throws<ObjectDisposedException>(() => { _ = buffer.PasswordSpan.Length; });
    }

    [Fact]
    public void SecurePasswordBuffer_Utf8Constructor_HoldsData_AndZeroesOnDispose()
    {
        byte[] utf8Secret = "SyntheticSecret999!"u8.ToArray();
        var buffer = new SecurePasswordBuffer(utf8Secret);

        Assert.Equal("SyntheticSecret999!", buffer.PasswordSpan.ToString());

        buffer.Dispose();

        Assert.True(buffer.IsDisposed);
        Assert.True(buffer.IsBackingBufferZeroed());
    }
}
