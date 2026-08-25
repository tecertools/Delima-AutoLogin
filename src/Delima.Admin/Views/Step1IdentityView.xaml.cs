using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Delima.Admin.Models;
using Delima.Admin.ViewModels;
using Microsoft.Win32;

namespace Delima.Admin.Views;

public partial class Step1IdentityView : UserControl
{
    public Step1IdentityView()
    {
        InitializeComponent();
    }

    private void OnSelectCrestClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Imej Lambang (*.png;*.jpg;*.svg)|*.png;*.jpg;*.jpeg;*.svg|Semua Fail (*.*)|*.*",
            Title = "Pilih Lambang Sekolah"
        };

        if (dlg.ShowDialog() == true && DataContext is Step1IdentityViewModel vm)
        {
            vm.CrestPath = dlg.FileName;
        }
    }

    private void OnClearCrestClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step1IdentityViewModel vm)
        {
            vm.ClearCrest();
        }
    }

    private void OnCrestDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnCrestDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
            e.Data.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length > 0 &&
            DataContext is Step1IdentityViewModel vm)
        {
            string file = files[0];
            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".svg" or ".ico")
            {
                vm.CrestPath = file;
            }
        }
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ThemePresetItem preset && DataContext is Step1IdentityViewModel vm)
        {
            vm.ApplyPreset(preset);
        }
    }

    private void OnPickColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ColorSwatchItem swatch)
            return;

        // Parse the current colour to pre-populate the dialog
        uint initial = 0x056839; // fallback: primary green
        if (TryParseHex(swatch.HexCode, out uint parsed))
            initial = parsed; // stored as 0x00BBGGRR for COLORREF

        if (NativeColorDialog.PickColor(initial, out uint chosen))
        {
            byte r = (byte)(chosen & 0xFF);
            byte g = (byte)((chosen >> 8) & 0xFF);
            byte b = (byte)((chosen >> 16) & 0xFF);
            swatch.HexCode = $"#{r:X2}{g:X2}{b:X2}";
        }
    }

    /// <summary>Converts #RRGGBB hex to Win32 COLORREF (0x00BBGGRR).</summary>
    private static bool TryParseHex(string? hex, out uint colorRef)
    {
        colorRef = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string clean = hex.Trim().TrimStart('#');
        if (clean.Length != 6) return false;
        if (!byte.TryParse(clean[0..2], System.Globalization.NumberStyles.HexNumber, null, out byte r)) return false;
        if (!byte.TryParse(clean[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g)) return false;
        if (!byte.TryParse(clean[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b)) return false;
        colorRef = (uint)(r | (g << 8) | (b << 16));
        return true;
    }
}

/// <summary>Wraps the Win32 ChooseColor common dialog (comdlg32.dll).</summary>
internal static class NativeColorDialog
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CHOOSECOLOR
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public uint rgbResult;
        public nint lpCustColors;
        public uint Flags;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
    }

    [DllImport("comdlg32.dll")]
    private static extern bool ChooseColor(ref CHOOSECOLOR cc);

    private static readonly uint[] _customColors = new uint[16];
    private static nint _custColorsHandle;

    static NativeColorDialog()
    {
        // Pin the custom colour array for the lifetime of the process
        var gch = System.Runtime.InteropServices.GCHandle.Alloc(_customColors, GCHandleType.Pinned);
        _custColorsHandle = gch.AddrOfPinnedObject();
    }

    /// <summary>
    /// Opens the native Windows colour picker.
    /// Returns true and sets <paramref name="chosenColorRef"/> if the user confirmed.
    /// </summary>
    public static bool PickColor(uint initialColorRef, out uint chosenColorRef)
    {
        const uint CC_FULLOPEN    = 0x0002;  // show spectrum panel immediately
        const uint CC_RGBINIT     = 0x0001;  // use rgbResult as initial colour
        const uint CC_ANYCOLOR    = 0x0100;

        var cc = new CHOOSECOLOR
        {
            lStructSize   = Marshal.SizeOf<CHOOSECOLOR>(),
            hwndOwner     = nint.Zero,
            rgbResult     = initialColorRef,
            lpCustColors  = _custColorsHandle,
            Flags         = CC_FULLOPEN | CC_RGBINIT | CC_ANYCOLOR
        };

        bool ok = ChooseColor(ref cc);
        chosenColorRef = ok ? cc.rgbResult : 0;
        return ok;
    }
}
