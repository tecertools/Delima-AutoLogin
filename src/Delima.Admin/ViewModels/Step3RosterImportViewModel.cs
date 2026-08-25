using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Import;

namespace Delima.Admin.ViewModels;

public sealed partial class Step3RosterImportViewModel : ObservableObject
{
    private readonly AdminWizardState _state;
    public const string DerivedGradeOption = "[ Diperoleh daripada Kelas ]";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFileLoaded))]
    [NotifyPropertyChangedFor(nameof(CanProceedToDryRun))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _filePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _activeSubView = "Mapping"; // "Mapping" or "DryRun"

    [ObservableProperty]
    private string _detectedEncoding = "UTF-8";

    [ObservableProperty]
    private string _diacriticPreview = "";

    public ObservableCollection<string> SourceHeaders { get; } = [];
    public ObservableCollection<string> GradeOptions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceedToDryRun))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string? _selectedFullNameCol;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceedToDryRun))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string? _selectedClassNameCol;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceedToDryRun))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string? _selectedGradeCol = DerivedGradeOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanProceedToDryRun))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string? _selectedDelimaIdCol;

    [ObservableProperty]
    private string? _selectedRegisterNoCol;

    public ObservableCollection<ImportedStudent> PreviewRows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyImport))]
    [NotifyPropertyChangedFor(nameof(DryRunSummaryText))]
    private DryRunReport? _dryRunReport;

    [ObservableProperty]
    private bool _isReadySectionExpanded = false;

    [ObservableProperty]
    private bool _isWarningsSectionExpanded = true;

    [ObservableProperty]
    private bool _isRejectsSectionExpanded = true;

    private List<RawImportRow> _rawFileRows = [];

    public string FileName => string.IsNullOrWhiteSpace(FilePath) ? "" : Path.GetFileName(FilePath);
    public int TotalRawRowCount => _rawFileRows.Count;

    public bool HasFileLoaded => !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);

    public bool CanProceedToDryRun => ValidateMapping(out _);

    public bool CanApplyImport => DryRunReport != null && DryRunReport.ValidCount > 0;

    public string DryRunSummaryText => DryRunReport?.GenerateSummaryText() ?? "";

    public string ValidationMessage
    {
        get
        {
            if (ActiveSubView == "Mapping")
            {
                ValidateMapping(out string msg);
                return msg;
            }
            else
            {
                if (DryRunReport == null || DryRunReport.ValidCount == 0)
                    return "Tiada murid yang sah untuk diimport.";
                return "";
            }
        }
    }

    public void ClearFile()
    {
        FilePath = "";
        _rawFileRows.Clear();
        SourceHeaders.Clear();
        PreviewRows.Clear();
        DryRunReport = null;
        ActiveSubView = "Mapping";
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(TotalRawRowCount));
        OnPropertyChanged(nameof(HasFileLoaded));
        OnPropertyChanged(nameof(CanProceedToDryRun));
        OnPropertyChanged(nameof(ValidationMessage));
    }

    public void GoToMappingView()
    {
        ActiveSubView = "Mapping";
    }

    public Step3RosterImportViewModel(AdminWizardState state)
    {
        _state = state;
        GradeOptions.Add(DerivedGradeOption);
    }

    public void LoadFile(string path)
    {
        if (!File.Exists(path)) return;

        FilePath = path;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string fileName = Path.GetFileName(path);

        var (headers, rows, totalRaw) = DataFileReader.ReadFile(stream, fileName);
        _rawFileRows = rows;

        SourceHeaders.Clear();
        GradeOptions.Clear();
        GradeOptions.Add(DerivedGradeOption);

        foreach (var h in headers)
        {
            SourceHeaders.Add(h);
            GradeOptions.Add(h);
        }

        // Auto-detect mappings
        var auto = ColumnMapping.AutoDetect(headers);
        SelectedFullNameCol = auto.FullNameColumn;
        SelectedClassNameCol = auto.ClassNameColumn;
        SelectedGradeCol = auto.GradeColumn ?? DerivedGradeOption;
        SelectedDelimaIdCol = auto.DelimaIdColumn;
        SelectedRegisterNoCol = auto.RegisterNoColumn;

        // Sniff encoding and sample diacritics
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            using var encStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var encoding = FileEncodingDetector.DetectEncoding(encStream);
            DetectedEncoding = encoding.EncodingName;
        }
        else
        {
            DetectedEncoding = "Excel (Unicode)";
        }

        // Extract a few preview names with diacritics
        ExtractDiacriticPreview();
        UpdatePreview();
    }

    private void ExtractDiacriticPreview()
    {
        if (string.IsNullOrEmpty(SelectedFullNameCol))
        {
            DiacriticPreview = "Pilih lajur nama untuk melihat pratonton.";
            return;
        }

        var sampleNames = _rawFileRows
            .Select(r => r.GetValue(SelectedFullNameCol))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(5)
            .ToList();

        if (sampleNames.Count > 0)
        {
            DiacriticPreview = string.Join(" • ", sampleNames);
        }
        else
        {
            DiacriticPreview = "Tiada nama dijumpai dalam lajur dipilih.";
        }
    }

    public void UpdatePreview()
    {
        PreviewRows.Clear();
        if (_rawFileRows.Count == 0 || !HasRequiredSelected()) return;

        string? gradeCol = SelectedGradeCol == DerivedGradeOption ? null : SelectedGradeCol;

        int rowNum = 1;
        foreach (var row in _rawFileRows.Take(10))
        {
            string rawName = row.GetValue(SelectedFullNameCol);
            string rawClass = row.GetValue(SelectedClassNameCol);
            string rawGrade = row.GetValue(gradeCol);
            string rawDelima = row.GetValue(SelectedDelimaIdCol);

            var (grade, cleanClass, _) = RosterImporter.NormalizeClassAndGrade(rawClass, rawGrade);
            var (_, emailLocal) = RosterImporter.NormalizeDelimaId(rawDelima);

            PreviewRows.Add(new ImportedStudent
            {
                RowNumber = rowNum++,
                FullName = rawName,
                ClassName = cleanClass,
                Grade = grade,
                EmailLocal = emailLocal ?? rawDelima
            });
        }
    }

    private bool HasRequiredSelected()
    {
        return !string.IsNullOrWhiteSpace(SelectedFullNameCol) &&
               !string.IsNullOrWhiteSpace(SelectedClassNameCol) &&
               !string.IsNullOrWhiteSpace(SelectedDelimaIdCol);
    }

    public bool ValidateMapping(out string message)
    {
        if (!HasFileLoaded)
        {
            message = "Sila muat naik fail CSV atau Excel.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedFullNameCol))
        {
            message = "Lajur 'Nama penuh' mesti dipetakan.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedClassNameCol))
        {
            message = "Lajur 'Kelas' mesti dipetakan.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedDelimaIdCol))
        {
            message = "Lajur 'ID DELIMa' mesti dipetakan.";
            return false;
        }

        message = "";
        return true;
    }

    public void SaveTemplate(string targetPath)
    {
        TemplateGenerator.SaveRosterTemplate(targetPath);
    }

    public void RunDryRunAnalysis()
    {
        if (!ValidateMapping(out _) || !HasFileLoaded) return;

        var mapping = new ColumnMapping
        {
            FullNameColumn = SelectedFullNameCol,
            ClassNameColumn = SelectedClassNameCol,
            GradeColumn = SelectedGradeCol == DerivedGradeOption ? null : SelectedGradeCol,
            DelimaIdColumn = SelectedDelimaIdCol,
            RegisterNoColumn = SelectedRegisterNoCol
        };

        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string fileName = Path.GetFileName(FilePath);

        var existingRosterStudents = _state.RosterStudents.Count > 0
            ? _state.RosterStudents.Select(s => new Delima.Core.Roster.Student
            {
                Id = s.Id,
                Name = s.FullName,
                ClassId = s.ClassName,
                EmailLocal = s.EmailLocal,
                DisplayName = s.DisplayName
            }).ToList()
            : null;

        DryRunReport = RosterImporter.AnalyzeDryRun(stream, fileName, mapping, existingRosterStudents);
        _state.LastDryRunReport = DryRunReport;
        ActiveSubView = "DryRun";

        OnPropertyChanged(nameof(CanApplyImport));
        OnPropertyChanged(nameof(ValidationMessage));
    }

    public void ExportRejectsCsv(string targetPath)
    {
        if (DryRunReport == null || DryRunReport.Rejects.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine("Baris,Nama,ID_Mentah,Sebab_Ditolak,Lajur");

        foreach (var r in DryRunReport.Rejects)
        {
            string name = r.StudentName.Replace("\"", "\"\"");
            string reason = r.Reason.Replace("\"", "\"\"");
            sb.AppendLine($"{r.RowNumber},\"{name}\",\"{r.RawId}\",\"{reason}\",{r.Field}");
        }

        File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
    }

    public void ApplyImport()
    {
        if (DryRunReport == null) return;

        _state.RosterStudents = DryRunReport.ReadyToImport.ToList();
    }
}
