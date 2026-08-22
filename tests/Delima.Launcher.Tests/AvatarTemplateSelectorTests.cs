using Delima.Launcher.Theming;

namespace Delima.Launcher.Tests;

public class AvatarTemplateSelectorTests
{
    [Theory]
    [InlineData("kucing", "cat")]
    [InlineData("cat", "cat")]
    [InlineData("buaya", "crocodile")]
    [InlineData("crocodile", "crocodile")]
    [InlineData("helang", "eagle")]
    [InlineData("eagle", "eagle")]
    [InlineData("gajah", "elephant")]
    [InlineData("elephant", "elephant")]
    [InlineData("memerang", "otter")]
    [InlineData("otter", "otter")]
    [InlineData("rakun", "raccoon")]
    [InlineData("raccoon", "raccoon")]
    [InlineData("kuda_belang", "zebra")]
    [InlineData("kuda belang", "zebra")]
    [InlineData("zebra", "zebra")]
    [InlineData("semut", "ant")]
    [InlineData("ant", "ant")]
    [InlineData("bizon", "bison")]
    [InlineData("bison", "bison")]
    [InlineData("ayam", "chicken")]
    [InlineData("chicken", "chicken")]
    [InlineData("anjing", "dog")]
    [InlineData("dog", "dog")]
    [InlineData("doraemon", "doraemon")]
    [InlineData("itik", "duck")]
    [InlineData("duck", "duck")]
    [InlineData("musang", "fox")]
    [InlineData("fox", "fox")]
    [InlineData("zirafah", "giraffe")]
    [InlineData("giraffe", "giraffe")]
    [InlineData("koala", "koala")]
    [InlineData("harimau_bintang", "leopard")]
    [InlineData("harimau bintang", "leopard")]
    [InlineData("leopard", "leopard")]
    [InlineData("tikus", "mouse")]
    [InlineData("mouse", "mouse")]
    [InlineData("penguin", "penguin")]
    [InlineData("pikachu", "pikachu")]
    [InlineData("biri_biri", "sheep")]
    [InlineData("biri biri", "sheep")]
    [InlineData("sheep", "sheep")]
    [InlineData("sloth", "sloth")]
    [InlineData("tiada", "missing")]
    [InlineData("?", "missing")]
    public void NormalizeAvatarKey_MapsAll22AvatarsCorrectly(string input, string expected)
    {
        string actual = AvatarTemplateSelector.NormalizeAvatarKey(input);
        Assert.Equal(expected, actual);
    }
}
