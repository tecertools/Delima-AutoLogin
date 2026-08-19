using System.Runtime.InteropServices;
using Delima.Win32;

namespace Delima.Win32.Tests;

public class NativeMethodsTests
{
    [Fact]
    public void InputStruct_Size_Is_40_Bytes_On_x64()
    {
        if (Environment.Is64BitProcess)
        {
            var size = Marshal.SizeOf<NativeMethods.INPUT>();
            Assert.Equal(40, size);
        }
    }

    [Fact]
    public void KeybdInputStruct_Size_Is_24_Bytes_On_x64()
    {
        if (Environment.Is64BitProcess)
        {
            var size = Marshal.SizeOf<NativeMethods.KEYBDINPUT>();
            Assert.Equal(24, size);
        }
    }
}
