using System.IO;
using Xunit;

namespace Delima.Win32.Tests;

public class ShortcutHelperTests
{
    [Fact]
    public void CreateShortcut_CreatesLnkFile_Successfully()
    {
        // Arrange
        string tempDir = Path.Combine(Path.GetTempPath(), "DelimaShortcutTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string targetPath = Path.Combine(tempDir, "DummyApp.exe");
            File.WriteAllText(targetPath, "dummy content");
            string shortcutPath = Path.Combine(tempDir, "TestShortcut.lnk");

            // Act
            ShortcutHelper.CreateShortcut(
                shortcutPath: shortcutPath,
                targetPath: targetPath,
                arguments: "--kiosk",
                workingDirectory: tempDir,
                description: "Test Shortcut");

            // Assert
            Assert.True(File.Exists(shortcutPath));
            var fileInfo = new FileInfo(shortcutPath);
            Assert.True(fileInfo.Length > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
