using System.Globalization;
using System.Windows.Media.Imaging;
using Delima.Launcher.Theming;
using Xunit;

namespace Delima.Launcher.Tests;

public class AvatarSeedToImageConverterTests
{
    [Fact]
    public void Convert_EmptyOrNullSeed_ReturnsPlaceholderWithoutException()
    {
        var converter = new AvatarSeedToImageConverter();
        var result = converter.Convert(string.Empty, typeof(BitmapImage), string.Empty, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        Assert.IsType<BitmapImage>(result);
    }

    [Fact]
    public void Convert_ValidSeed_DoesNotThrowInvalidOperationException()
    {
        var converter = new AvatarSeedToImageConverter();
        var result = converter.Convert("test_student_seed_123", typeof(BitmapImage), string.Empty, CultureInfo.InvariantCulture);
        Assert.NotNull(result);
        Assert.IsType<BitmapImage>(result);
    }
}
