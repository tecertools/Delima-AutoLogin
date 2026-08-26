using System.IO;
using System.Threading.Tasks;
using Delima.Core.Services;
using Xunit;

namespace Delima.Core.Tests;

public class DiceBearServiceTests
{
    [Fact]
    public void GenerateFallbackPng_ProducesValidPngBytesWithCorrectHeader()
    {
        byte[] bytes = DiceBearService.GenerateFallbackPng("test_student_seed_456");

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 100);

        // Standard PNG signature: 89 50 4E 47 0D 0A 1A 0A
        byte[] expectedHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.Equal(expectedHeader, bytes.Take(8));
    }

    [Fact]
    public void GetLocalOrRemoteUri_AlwaysReturnsFileUriExistingOnDisk()
    {
        string seed = "student_offline_test_" + Guid.NewGuid().ToString("N")[..8];
        string uri = DiceBearService.GetLocalOrRemoteUri(seed);

        Assert.NotNull(uri);
        Assert.StartsWith("file://", uri, StringComparison.OrdinalIgnoreCase);

        string localPath = new Uri(uri).LocalPath;
        Assert.True(File.Exists(localPath));
        Assert.True(new FileInfo(localPath).Length > 0);
    }

    [Fact]
    public async Task EnsureCachedAsync_ReturnsValidPath()
    {
        string seed = "student_cache_test_" + Guid.NewGuid().ToString("N")[..8];
        string? path = await DiceBearService.EnsureCachedAsync(seed);

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path!).Length > 0);
    }
}
