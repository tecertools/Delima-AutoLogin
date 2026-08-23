using System.IO;
using System.Text;
using Delima.Admin.Models;
using Delima.Admin.ViewModels;
using Delima.Import;

namespace Delima.Admin.Tests;

public class Step4PasswordImportTests
{
    [Fact]
    public void Consent_RequiresExactSchoolCode()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234" }
        };

        var vm = new Step4PasswordImportViewModel(state);

        Assert.Equal("Consent", vm.ActiveSubView);
        Assert.False(vm.CanProceedConsent);

        vm.ConsentTypedCode = "WRONG";
        Assert.False(vm.CanProceedConsent);

        vm.ConsentTypedCode = "BBA1234";
        Assert.True(vm.CanProceedConsent);

        vm.AcknowledgeConsent();
        Assert.Equal("Grid", vm.ActiveSubView);
        Assert.True(state.HasAcknowledgedConsent);
    }

    [Fact]
    public void LoadPasswordFile_DetectsSharedPasswordsAndUpdatesGrid()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234" },
            HasAcknowledgedConsent = true,
            AdminPassphrase = "SecretPassphrase2026!"
        };

        state.RosterStudents.Add(new ImportedStudent { Id = "s_12345678", FullName = "Pupil One", ClassName = "2C", DelimaDigits = "12345678", EmailLocal = "m-12345678" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_12345679", FullName = "Pupil Two", ClassName = "2C", DelimaDigits = "12345679", EmailLocal = "m-12345679" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_12345680", FullName = "Pupil Three", ClassName = "2C", DelimaDigits = "12345680", EmailLocal = "m-12345680" });

        var vm = new Step4PasswordImportViewModel(state);

        string pwdCsv = Path.Combine(Path.GetTempPath(), $"pwd_test_{Guid.NewGuid():N}.csv");
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("id_delima,kata_laluan");
            sb.AppendLine("m-12345678,SharedSecret123!");
            sb.AppendLine("m-12345679,SharedSecret123!"); // Same password -> shared!
            sb.AppendLine("m-12345680,UniqueSecret456!");
            File.WriteAllText(pwdCsv, sb.ToString(), Encoding.UTF8);

            vm.LoadPasswordFile(pwdCsv);

            Assert.Equal(3, vm.WithPasswordCount);
            Assert.Equal(2, vm.SharedPasswordCount);

            var item1 = vm.PasswordItems.First(p => p.DelimaDigits == "12345678");
            Assert.True(item1.IsShared);
            Assert.Equal("Dikongsi", item1.StatusBadge);
            Assert.Equal("••••••••", item1.MaskedText);
        }
        finally
        {
            if (File.Exists(pwdCsv)) File.Delete(pwdCsv);
        }
    }

    [Fact]
    public void VerifyAndReveal_ChecksPassphraseAndRevealsSingleRow()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234" },
            HasAcknowledgedConsent = true,
            AdminPassphrase = "MasterSecret1234!"
        };

        state.RosterStudents.Add(new ImportedStudent { Id = "s_12345678", FullName = "Pupil One", ClassName = "2C", DelimaDigits = "12345678", EmailLocal = "m-12345678" });
        state.StudentPasswords["s_12345678"] = "StudentPass999!";

        var vm = new Step4PasswordImportViewModel(state);
        var item = vm.PasswordItems[0];

        vm.OpenRevealPopover(item);
        Assert.True(vm.IsPopoverOpen);

        // Wrong passphrase
        bool fail = vm.VerifyAndReveal("WrongPassphrase");
        Assert.False(fail);
        Assert.False(item.IsRevealed);
        Assert.Equal("••••••••", item.MaskedText);
        Assert.Contains("salah", vm.PopoverError);

        // Correct passphrase
        bool success = vm.VerifyAndReveal("MasterSecret1234!");
        Assert.True(success);
        Assert.True(item.IsRevealed);
        Assert.Equal("StudentPass999!", item.MaskedText);
        Assert.Equal(10, item.RevealCountdownSeconds);
    }

    [Fact]
    public void SavePasswordTemplate_CreatesValidCsvWithRosterPrepopulated()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234" }
        };
        state.RosterStudents.Add(new ImportedStudent { Id = "s_12345678", FullName = "Danial", ClassName = "2C", DelimaDigits = "12345678", EmailLocal = "m-12345678", RegisterNoJoinKey = "170101-10-1234" });

        var vm = new Step4PasswordImportViewModel(state);
        string pwdTemplatePath = Path.Combine(Path.GetTempPath(), $"pwd_template_{Guid.NewGuid():N}.csv");
        try
        {
            vm.SavePasswordTemplate(pwdTemplatePath);
            Assert.True(File.Exists(pwdTemplatePath));
            string content = File.ReadAllText(pwdTemplatePath);
            Assert.Contains("KATA LALUAN", content);
            Assert.Contains("Danial", content);
            Assert.Contains("m-12345678", content);
        }
        finally
        {
            if (File.Exists(pwdTemplatePath)) File.Delete(pwdTemplatePath);
        }
    }
}
