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

    [Fact]
    public void YearFilter_FiltersPasswordItemsAndUpdatesClassesDynamically()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234" },
            HasAcknowledgedConsent = true
        };

        state.RosterStudents.Add(new ImportedStudent { Id = "s_1", FullName = "Ali", ClassName = "1 Amanah", Grade = 1, EmailLocal = "m-1" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_2", FullName = "Abu", ClassName = "1 Bakti", Grade = 1, EmailLocal = "m-2" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_3", FullName = "Siti", ClassName = "2 Bestari", Grade = 2, EmailLocal = "m-3" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_4", FullName = "Chong", ClassName = "3 Cemerlang", Grade = 3, EmailLocal = "m-4" });

        var vm = new Step4PasswordImportViewModel(state);

        // Year options populated
        Assert.Contains("Semua Tahun", vm.YearNames);
        Assert.Contains("Tahun 1", vm.YearNames);
        Assert.Contains("Tahun 2", vm.YearNames);
        Assert.Contains("Tahun 3", vm.YearNames);

        // Initially Semua Tahun
        Assert.Equal(4, vm.FilteredPasswordItems.Count);

        // Filter by Tahun 1
        vm.SelectedYearFilter = "Tahun 1";
        Assert.Equal(2, vm.FilteredPasswordItems.Count);
        Assert.Contains("Semua Kelas", vm.ClassNames);
        Assert.Contains("1 Amanah", vm.ClassNames);
        Assert.Contains("1 Bakti", vm.ClassNames);
        Assert.DoesNotContain("2 Bestari", vm.ClassNames);

        // Further filter by Class 1 Amanah
        vm.SelectedClassFilter = "1 Amanah";
        Assert.Single(vm.FilteredPasswordItems);
        Assert.Equal("Ali", vm.FilteredPasswordItems[0].StudentName);

        // Reset to Semua Tahun
        vm.SelectedYearFilter = "Semua Tahun";
        Assert.Equal(4, vm.FilteredPasswordItems.Count);
    }

    [Fact]
    public void SavePasswordTemplate_WithYearAndClassFilters_ExportsScopedSubset()
    {
        var state = new AdminWizardState
        {
            School = new Delima.Core.Store.SchoolInfo { Code = "BBA1234" },
            HasAcknowledgedConsent = true
        };

        state.RosterStudents.Add(new ImportedStudent { Id = "s_1", FullName = "Danial", ClassName = "1 Amanah", Grade = 1, EmailLocal = "m-1" });
        state.RosterStudents.Add(new ImportedStudent { Id = "s_2", FullName = "Aishah", ClassName = "2 Bestari", Grade = 2, EmailLocal = "m-2" });

        var vm = new Step4PasswordImportViewModel(state);
        string templatePath = Path.Combine(Path.GetTempPath(), $"scoped_template_{Guid.NewGuid():N}.csv");
        try
        {
            vm.SelectedYearFilter = "Tahun 1";
            vm.SelectedClassFilter = "Semua Kelas";
            vm.SavePasswordTemplate(templatePath);

            Assert.True(File.Exists(templatePath));
            string content = File.ReadAllText(templatePath);
            Assert.Contains("TAHUN", content);
            Assert.Contains("Danial", content);
            Assert.DoesNotContain("Aishah", content);
        }
        finally
        {
            if (File.Exists(templatePath)) File.Delete(templatePath);
        }
    }
}

