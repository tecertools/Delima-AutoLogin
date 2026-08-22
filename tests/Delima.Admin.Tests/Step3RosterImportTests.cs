using System.IO;
using System.Text;
using Delima.Admin.Models;
using Delima.Admin.ViewModels;

namespace Delima.Admin.Tests;

public class Step3RosterImportTests
{
    private readonly string _testCsvPath;

    public Step3RosterImportTests()
    {
        _testCsvPath = Path.Combine(Path.GetTempPath(), $"roster_test_{Guid.NewGuid():N}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("No,Nama Pelajar,Kelas,Tahun,ID DELIMa,No KP");
        sb.AppendLine("1,Nur Aishah Binti Ahmad,2 Cemerlang,2,m-12345678,050101-10-1234");
        sb.AppendLine("2,Tan Wei Ming,2 Cemerlang,2,m-12345679,050202-10-5678");
        sb.AppendLine("3,Arjun A/L Kumaran,2 Cemerlang,2,m-12345680,050303-10-9012");
        sb.AppendLine("4,Duplicate Pupil,2 Cemerlang,2,m-12345678,050404-10-1111"); // Duplicate ID
        sb.AppendLine("5,Malformed Pupil,2 Cemerlang,2,invalid-id,050505-10-2222"); // Malformed ID
        File.WriteAllText(_testCsvPath, sb.ToString(), Encoding.UTF8);
    }

    [Fact]
    public void LoadFile_AutoDetectsHeadersAndGeneratesPreview()
    {
        var state = new AdminWizardState();
        var vm = new Step3RosterImportViewModel(state);

        vm.LoadFile(_testCsvPath);

        Assert.True(vm.HasFileLoaded);
        Assert.Equal("Nama Pelajar", vm.SelectedFullNameCol);
        Assert.Equal("Kelas", vm.SelectedClassNameCol);
        Assert.Equal("ID DELIMa", vm.SelectedDelimaIdCol);
        Assert.Equal("Tahun", vm.SelectedGradeCol);
        Assert.Equal("No KP", vm.SelectedRegisterNoCol);

        // Preview should have rows loaded
        Assert.True(vm.PreviewRows.Count > 0);
        Assert.Equal("Nur Aishah Binti Ahmad", vm.PreviewRows[0].FullName);
        Assert.Equal("2 Cemerlang", vm.PreviewRows[0].ClassName);
    }

    [Fact]
    public void RunDryRunAnalysis_SeparatesValidWarningsAndRejects()
    {
        var state = new AdminWizardState();
        var vm = new Step3RosterImportViewModel(state);

        vm.LoadFile(_testCsvPath);
        vm.RunDryRunAnalysis();

        Assert.NotNull(vm.DryRunReport);
        Assert.Equal(3, vm.DryRunReport.ValidCount);
        Assert.Single(vm.DryRunReport.Warnings); // 1 duplicate warning
        Assert.Single(vm.DryRunReport.Rejects); // 1 malformed reject
        Assert.Equal("DryRun", vm.ActiveSubView);
        Assert.True(vm.CanApplyImport);

        // Apply import
        vm.ApplyImport();
        Assert.Equal(3, state.RosterStudents.Count);
    }

    [Fact]
    public void ExportRejectsCsv_WritesValidFormattedFile()
    {
        var state = new AdminWizardState();
        var vm = new Step3RosterImportViewModel(state);

        vm.LoadFile(_testCsvPath);
        vm.RunDryRunAnalysis();

        string rejectOutPath = Path.Combine(Path.GetTempPath(), $"rejects_out_{Guid.NewGuid():N}.csv");
        try
        {
            vm.ExportRejectsCsv(rejectOutPath);
            Assert.True(File.Exists(rejectOutPath));
            string content = File.ReadAllText(rejectOutPath);
            Assert.Contains("Malformed", content);
        }
        finally
        {
            if (File.Exists(rejectOutPath)) File.Delete(rejectOutPath);
        }
    }

    [Fact]
    public void SaveTemplate_CreatesValidCsvFile()
    {
        var state = new AdminWizardState();
        var vm = new Step3RosterImportViewModel(state);

        string templatePath = Path.Combine(Path.GetTempPath(), $"roster_template_{Guid.NewGuid():N}.csv");
        try
        {
            vm.SaveTemplate(templatePath);
            Assert.True(File.Exists(templatePath));
            string content = File.ReadAllText(templatePath);
            Assert.Contains("NAMA MURID", content);
            Assert.Contains("ID DELIMA", content);
        }
        finally
        {
            if (File.Exists(templatePath)) File.Delete(templatePath);
        }
    }
}
