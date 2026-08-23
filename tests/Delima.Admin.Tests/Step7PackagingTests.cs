using System.IO;
using Delima.Admin.Models;
using Delima.Admin.ViewModels;
using Delima.Core.Crypto;
using Delima.Core.Store;
using Delima.Import;

namespace Delima.Admin.Tests;

public class Step7PackagingTests
{
    [Fact]
    public void BuildMasterBundle_CreatesValidEncryptedPack_ThatUnpacksCorrectly()
    {
        var state = new AdminWizardState
        {
            School = new SchoolInfo { Code = "SKS24", Name = "SK Seksyen 24", Domain = "moe-dl.edu.my" },
            AdminPassphrase = "MasterKeyPassphrase2026!"
        };

        state.RosterStudents.Add(new ImportedStudent
        {
            Id = "s_12345678",
            FullName = "Nur Aishah Binti Ahmad",
            ClassName = "2 Cemerlang",
            Grade = 2,
            DelimaDigits = "12345678",
            EmailLocal = "m-12345678"
        });
        state.StudentPasswords["s_12345678"] = "SecretPassword123!";

        var vm = new Step7ProvisionViewModel(state);
        byte[] packBytes = vm.BuildMasterBundle();

        Assert.NotNull(packBytes);
        Assert.True(packBytes.Length > MasterBundleHeader.HeaderSizeBytes + MasterBundleHeader.TagSizeBytes);

        // Unpack and verify
        var payload = MasterBundle.Unpack(packBytes, "MasterKeyPassphrase2026!");

        Assert.Equal(2, payload.SchemaVersion);
        Assert.Equal("SKS24", payload.School.Code);
        Assert.Equal("SK Seksyen 24", payload.School.Name);
        Assert.Single(payload.Classes);
        Assert.Equal("2 Cemerlang", payload.Classes[0].Name);
        Assert.Single(payload.Students);
        Assert.Equal("Nur Aishah Binti Ahmad", payload.Students[0].Name);
        Assert.Equal("SecretPassword123!", payload.Students[0].Password);
        Assert.NotNull(payload.Students[0].PicturePassword);
        Assert.True(PicturePasswordHasher.VerifyPicturePassword(["kucing", "bunga", "kereta"], payload.Students[0].PicturePassword));
    }

    [Fact]
    public void PowerShellScript_ContainsRequiredStdinPipingAndParams()
    {
        var state = new AdminWizardState();
        var vm = new Step7ProvisionViewModel(state);

        Assert.Contains("--passphrase-stdin", vm.PowerShellScript);
        Assert.Contains("Delima.Provision.exe", vm.PowerShellScript);
        Assert.Contains("Read-Host", vm.PowerShellScript);
    }

    [Fact]
    public void ExportChecklistCsv_GeneratesValidCsv()
    {
        var state = new AdminWizardState();
        var vm = new Step7ProvisionViewModel(state);

        string checklistPath = Path.Combine(Path.GetTempPath(), $"checklist_{Guid.NewGuid():N}.csv");
        try
        {
            vm.ExportChecklistCsv(checklistPath);
            Assert.True(File.Exists(checklistPath));
            string content = File.ReadAllText(checklistPath);
            Assert.Contains("MAKMAL-01", content);
            Assert.Contains("2.0.0", content);
            Assert.Contains("AppLocker_Disahkan", content);
        }
        finally
        {
            if (File.Exists(checklistPath)) File.Delete(checklistPath);
        }
    }
}
