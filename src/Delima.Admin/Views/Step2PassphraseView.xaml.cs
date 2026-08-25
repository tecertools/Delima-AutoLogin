using System.IO;
using System.Windows;
using System.Windows.Controls;
using Delima.Admin.ViewModels;
using Microsoft.Win32;

namespace Delima.Admin.Views;

public partial class Step2PassphraseView : UserControl
{
    private bool _isUpdating;

    public Step2PassphraseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step2PassphraseViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (_isUpdating) return;
                if (args.PropertyName == nameof(Step2PassphraseViewModel.Passphrase))
                {
                    if (PwdBox != null && PwdBox.Password != vm.Passphrase)
                        PwdBox.Password = vm.Passphrase;
                }
                else if (args.PropertyName == nameof(Step2PassphraseViewModel.ConfirmPassphrase))
                {
                    if (ConfirmPwdBox != null && ConfirmPwdBox.Password != vm.ConfirmPassphrase)
                        ConfirmPwdBox.Password = vm.ConfirmPassphrase;
                }
            };

            if (PwdBox != null) PwdBox.Password = vm.Passphrase;
            if (ConfirmPwdBox != null) ConfirmPwdBox.Password = vm.ConfirmPassphrase;
        }
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step2PassphraseViewModel vm && !_isUpdating)
        {
            _isUpdating = true;
            vm.Passphrase = PwdBox.Password;
            _isUpdating = false;
        }
    }

    private void ConfirmPwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step2PassphraseViewModel vm && !_isUpdating)
        {
            _isUpdating = true;
            vm.ConfirmPassphrase = ConfirmPwdBox.Password;
            _isUpdating = false;
        }
    }

    private void OnCopyKcvClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step2PassphraseViewModel vm && vm.RecoverySheet != null)
        {
            try
            {
                Clipboard.SetText(vm.RecoverySheet.KeyCheckValue);
                vm.CopyFeedbackMessage = $"✓ Cap Jari KCV [{vm.RecoverySheet.KeyCheckValue}] berjaya disalin!";
            }
            catch
            {
                // Clipboard fallback
            }
        }
    }

    private void OnCopyFullSheetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step2PassphraseViewModel vm && vm.RecoverySheet != null)
        {
            try
            {
                string text = vm.GetFormattedRecoverySheetText();
                Clipboard.SetText(text);
                vm.CopyFeedbackMessage = "✓ Seluruh maklumat helaian pemulihan berjaya disalin!";
            }
            catch
            {
                // Clipboard fallback
            }
        }
    }

    private void OnSaveSheetFileClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is Step2PassphraseViewModel vm && vm.RecoverySheet != null)
        {
            var dlg = new SaveFileDialog
            {
                FileName = $"Helaian_Pemulihan_DELIMa_{vm.RecoverySheet.SchoolCode}.txt",
                Filter = "Fail Teks (*.txt)|*.txt|Semua Fail (*.*)|*.*",
                Title = "Simpan Helaian Pemulihan Pentadbir"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, vm.GetFormattedRecoverySheetText());
                    vm.CopyFeedbackMessage = $"✓ Fail berjaya disimpan ke: {Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gagal menyimpan fail: {ex.Message}", "Ralat Simpan", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

