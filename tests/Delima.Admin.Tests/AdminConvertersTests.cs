using System.Globalization;
using System.Windows;
using Delima.Admin.Converters;
using Delima.Admin.Models;
using Xunit;

namespace Delima.Admin.Tests;

public class AdminConvertersTests
{
    [Fact]
    public void BooleanToVisibilityConverter_SupportsInvertPropertyAndParameter()
    {
        var conv = new BooleanToVisibilityConverter();

        // Default (Invert = false, parameter = "")
        Assert.Equal(Visibility.Visible, conv.Convert(true, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, conv.Convert(false, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture));

        // Parameter Invert
        Assert.Equal(Visibility.Collapsed, conv.Convert(true, typeof(Visibility), "Invert", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, conv.Convert(false, typeof(Visibility), "Invert", CultureInfo.InvariantCulture));

        // Property Invert
        conv.Invert = true;
        Assert.Equal(Visibility.Collapsed, conv.Convert(true, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, conv.Convert(false, typeof(Visibility), string.Empty, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void InverseBooleanConverter_InvertsBooleanValues()
    {
        var conv = new InverseBooleanConverter();
        Assert.False((bool)conv.Convert(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture));
        Assert.True((bool)conv.Convert(false, typeof(bool), string.Empty, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void StepNumberDisplayConverter_ReturnsCheckmarkForDone_AndNumberOtherwise()
    {
        var conv = new StepNumberDisplayConverter();

        var itemDone = new StepNavigationItem { StepNumber = 3, Status = StepStatus.Done };
        Assert.Equal("✓", conv.Convert(itemDone, typeof(string), string.Empty, CultureInfo.InvariantCulture));

        var itemInProgress = new StepNavigationItem { StepNumber = 3, Status = StepStatus.InProgress };
        Assert.Equal("3", conv.Convert(itemInProgress, typeof(string), string.Empty, CultureInfo.InvariantCulture));

        var itemLocked = new StepNavigationItem { StepNumber = 5, Status = StepStatus.Locked };
        Assert.Equal("5", conv.Convert(itemLocked, typeof(string), string.Empty, CultureInfo.InvariantCulture));
    }
}
