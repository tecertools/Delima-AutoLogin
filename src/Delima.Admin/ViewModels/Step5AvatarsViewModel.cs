using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;
using Delima.Core.Services;
using Delima.Import;

namespace Delima.Admin.ViewModels;

public sealed partial class AvatarAssignmentItem : ObservableObject
{
    public string StudentId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public int Grade { get; init; }
    public string EmailLocal { get; init; } = "";

    public string FullClassDisplay
    {
        get
        {
            if (Grade <= 0) return string.IsNullOrWhiteSpace(ClassName) ? "Tanpa Kelas" : ClassName;
            string trimmed = ClassName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(trimmed)) return $"Tahun {Grade}";
            if (trimmed.StartsWith(Grade.ToString()) || trimmed.StartsWith($"Tahun {Grade}", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            return $"{Grade} {trimmed}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvatarUrl))]
    private string _avatar = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PicturePasswordDisplay))]
    [NotifyPropertyChangedFor(nameof(PicturePasswordDetailedDisplay))]
    private List<string> _picturePassword = ["kucing", "bunga", "kereta"];

    /// <summary>
    /// The effective DiceBear seed — falls back to StudentId for blank/legacy animal keys.
    /// </summary>
    public string DiceBearSeed => DiceBearService.ResolveSeed(Avatar, StudentId);

    /// <summary>
    /// Full HTTPS URL to the student's Critters avatar PNG.
    /// </summary>
    public string AvatarUrl => DiceBearService.GetAvatarUrl(DiceBearSeed);

    public string PicturePasswordDisplay => string.Join(" ➔ ", PicturePassword.Select(GetPictureIconEmoji));

    public string PicturePasswordDetailedDisplay => string.Join(" ➔ ", PicturePassword.Select(p => $"{GetPictureIconEmoji(p)} {GetAvatarDisplayName(p)}"));

    /// <summary>Returns emoji for picture-password icons only (not used for student avatars).</summary>
    public static string GetPictureIconEmoji(string icon) => (icon?.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_")) switch
    {
        "kucing" or "cat" => "🐱",
        "buaya" or "crocodile" => "🐊",
        "helang" or "eagle" => "🦅",
        "gajah" or "elephant" => "🐘",
        "memerang" or "otter" => "🦦",
        "rakun" or "raccoon" => "🦝",
        "kuda_belang" or "kudabelang" or "zebra" => "🦓",
        "semut" or "ant" => "🐜",
        "bizon" or "bison" or "seladang" => "🦬",
        "ayam" or "chicken" => "🐔",
        "anjing" or "dog" or "puppy" => "🐶",
        "doraemon" => "🐱",
        "itik" or "duck" => "🦆",
        "musang" or "fox" or "rubah" => "🦊",
        "zirafah" or "giraffe" => "🦒",
        "koala" => "🐨",
        "harimau_bintang" or "harimaubintang" or "leopard" => "🐆",
        "tikus" or "mouse" => "🐭",
        "penguin" => "🐧",
        "pikachu" => "⚡",
        "biri_biri" or "biribiri" or "sheep" or "kambing" => "🐑",
        "sloth" => "🦥",
        "ikan" or "fish" => "🐟",
        "bintang" or "star" => "⭐",
        "burung" or "bird" => "🐦",
        "kereta" or "car" => "🚗",
        "bunga" or "flower" => "🌸",
        "rama_rama" or "butterfly" => "🦋",
        "epal" or "apple" => "🍎",
        "pokok" or "tree" => "🌳",
        "bola" or "ball" => "⚽",
        "awan" or "cloud" => "☁️",
        "matahari" or "sun" => "☀️",
        "buku" or "book" => "📖",
        "pensel" or "pencil" => "✏️",
        "kapal" or "ship" => "🚢",
        "bulan" or "moon" => "🌙",
        "rumah" or "house" => "🏠",
        "payung" => "☂️",
        "belon" => "🎈",
        "pisang" => "🍌",
        "jam" => "⏰",
        "topi" => "🎩",
        _ => "🎴"
    };

    public static string GetAvatarDisplayName(string avatar) => (avatar?.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_")) switch
    {
        "kucing" or "cat" => "Kucing",
        "buaya" or "crocodile" => "Buaya",
        "helang" or "eagle" => "Helang",
        "gajah" or "elephant" => "Gajah",
        "memerang" or "otter" => "Memerang",
        "rakun" or "raccoon" => "Rakun",
        "kuda_belang" or "kudabelang" or "zebra" => "Kuda Belang",
        "semut" or "ant" => "Semut",
        "bizon" or "bison" or "seladang" => "Bizon",
        "ayam" or "chicken" => "Ayam",
        "anjing" or "dog" or "puppy" => "Anjing",
        "doraemon" => "Doraemon",
        "itik" or "duck" => "Itik",
        "musang" or "fox" or "rubah" => "Musang",
        "zirafah" or "giraffe" => "Zirafah",
        "koala" => "Koala",
        "harimau_bintang" or "harimaubintang" or "leopard" => "Harimau Bintang",
        "tikus" or "mouse" => "Tikus",
        "penguin" => "Penguin",
        "pikachu" => "Pikachu",
        "biri_biri" or "biribiri" or "sheep" or "kambing" => "Biri-biri",
        "sloth" => "Sloth",
        "ikan" or "fish" => "Ikan",
        "bintang" or "star" => "Bintang",
        "burung" or "bird" => "Burung",
        "kereta" or "car" => "Kereta",
        "bunga" or "flower" => "Bunga",
        "rama_rama" or "butterfly" => "Rama-rama",
        "epal" or "apple" => "Epal",
        "pokok" or "tree" => "Pokok",
        "bola" or "ball" => "Bola",
        "awan" or "cloud" => "Awan",
        "matahari" or "sun" => "Matahari",
        "buku" or "book" => "Buku",
        "pensel" or "pencil" => "Pensel",
        "kapal" or "ship" => "Kapal",
        "bulan" or "moon" => "Bulan",
        "rumah" or "house" => "Rumah",
        "payung" => "Payung",
        "belon" => "Belon",
        "pisang" => "Pisang",
        "jam" => "Jam",
        "topi" => "Topi",
        _ => avatar ?? "Avatar"
    };
}

public sealed partial class Step5AvatarsViewModel : ObservableObject
{
    private readonly AdminWizardState _state;

    public static readonly string[] StandardPicturePasswordIcons =
    [
        "kucing", "bunga", "kereta", "bola", "ikan", "bintang",
        "epal", "payung", "belon", "pokok", "rumah", "buku",
        "pisang", "jam", "pensel", "topi"
    ];

    public ObservableCollection<AvatarAssignmentItem> AvatarItems { get; } = [];
    public ObservableCollection<AvatarAssignmentItem> FilteredAvatarItems { get; } = [];
    public ObservableCollection<string> YearNames { get; } = [];
    public ObservableCollection<string> ClassNames { get; } = [];

    [ObservableProperty]
    private string? _selectedYearFilter;

    [ObservableProperty]
    private string? _selectedClassFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWarning))]
    private bool _picturePasswordRequired = true;

    public bool ShowWarning => !PicturePasswordRequired;

    public bool CanProceed => true;

    public Step5AvatarsViewModel(AdminWizardState state)
    {
        _state = state;
        _picturePasswordRequired = state.Config.PicturePasswordRequired;

        InitializeAvatars();
    }

    partial void OnSelectedYearFilterChanged(string? value)
    {
        UpdateClassListForSelectedYear();
        UpdateFilteredAvatarItems();
    }

    partial void OnSelectedClassFilterChanged(string? value)
    {
        UpdateFilteredAvatarItems();
    }

    private void UpdateClassListForSelectedYear()
    {
        string? previousClassSelection = SelectedClassFilter;
        ClassNames.Clear();

        int filterGrade = ParseYearFilterToGrade(SelectedYearFilter);

        var query = AvatarItems.AsEnumerable();
        if (filterGrade > 0)
        {
            query = query.Where(a => a.Grade == filterGrade);
        }

        var classes = query.Select(s => s.ClassName)
                           .Where(c => !string.IsNullOrWhiteSpace(c))
                           .Distinct()
                           .OrderBy(c => c)
                           .ToList();

        ClassNames.Add("Semua Kelas");
        foreach (var c in classes)
        {
            ClassNames.Add(c);
        }

        SelectedClassFilter = "Semua Kelas";
    }

    public static int ParseYearFilterToGrade(string? yearFilter)
    {
        if (string.IsNullOrWhiteSpace(yearFilter) || yearFilter.Equals("Semua Tahun", StringComparison.OrdinalIgnoreCase))
            return 0;

        string digits = new(yearFilter.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int g) ? g : 0;
    }

    public void UpdateFilteredAvatarItems()
    {
        FilteredAvatarItems.Clear();
        int filterGrade = ParseYearFilterToGrade(SelectedYearFilter);
        var classFilter = SelectedClassFilter;

        IEnumerable<AvatarAssignmentItem> items = AvatarItems;

        if (filterGrade > 0)
        {
            items = items.Where(a => a.Grade == filterGrade);
        }

        if (!string.IsNullOrWhiteSpace(classFilter) && !classFilter.Equals("Semua Kelas", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(a => string.Equals(a.ClassName, classFilter, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredAvatarItems.Add(item);
        }
    }

    public void InitializeAvatars()
    {
        AvatarItems.Clear();
        YearNames.Clear();
        ClassNames.Clear();

        // Populate YearNames (Semua Tahun, Tahun 1..6)
        var distinctGrades = _state.RosterStudents.Select(s => s.Grade > 0 ? s.Grade : RosterImporter.NormalizeClassAndGrade(s.ClassName, null).Grade)
                                                 .Where(g => g >= 1 && g <= 6)
                                                 .Distinct()
                                                 .OrderBy(g => g)
                                                 .ToList();

        YearNames.Add("Semua Tahun");
        if (distinctGrades.Count > 0)
        {
            foreach (var g in distinctGrades)
            {
                YearNames.Add($"Tahun {g}");
            }
        }
        else
        {
            for (int g = 1; g <= 6; g++)
            {
                YearNames.Add($"Tahun {g}");
            }
        }

        // Group by (Grade, ClassName) and assign unique avatars per class
        var groupedStudents = _state.RosterStudents.GroupBy(s => (
            Grade: s.Grade > 0 ? s.Grade : RosterImporter.NormalizeClassAndGrade(s.ClassName, null).Grade,
            ClassName: s.ClassName ?? ""
        ));

        foreach (var group in groupedStudents)
        {
            int avatarIdx = 0;
            int grade = group.Key.Grade;
            foreach (var student in group)
            {
                string avatar = "";
                if (_state.StudentAvatars.TryGetValue(student.Id, out var existingAvatar)
                    && !string.IsNullOrWhiteSpace(existingAvatar)
                    && !DiceBearService.IsLegacyKey(existingAvatar))
                {
                    avatar = existingAvatar;
                }

                List<string> picPassword;
                if (_state.StudentPicturePasswords.TryGetValue(student.Id, out var existingPic) && existingPic.Count == 3)
                {
                    picPassword = [.. existingPic];
                }
                else
                {
                    int total = StandardPicturePasswordIcons.Length;
                    int i1 = avatarIdx % total;
                    int i2 = (avatarIdx * 3 + 1) % total;
                    if (i2 == i1) i2 = (i2 + 1) % total;
                    int i3 = (avatarIdx * 7 + 2) % total;
                    while (i3 == i1 || i3 == i2) i3 = (i3 + 1) % total;
                    picPassword = [StandardPicturePasswordIcons[i1], StandardPicturePasswordIcons[i2], StandardPicturePasswordIcons[i3]];
                }

                avatarIdx++;

                AvatarItems.Add(new AvatarAssignmentItem
                {
                    StudentId = student.Id,
                    StudentName = student.FullName,
                    ClassName = student.ClassName,
                    Grade = grade,
                    EmailLocal = student.EmailLocal,
                    Avatar = avatar,
                    PicturePassword = picPassword
                });
            }
        }

        SelectedYearFilter = "Semua Tahun";
        UpdateClassListForSelectedYear();
        UpdateFilteredAvatarItems();

        var seeds = AvatarItems.Select(a => a.DiceBearSeed).ToList();
        _ = DiceBearService.PreCacheBatchAsync(seeds);
    }

    public void RandomizeAvatar(AvatarAssignmentItem item)
    {
        item.Avatar = Guid.NewGuid().ToString("N")[..10];
        _ = DiceBearService.EnsureCachedAsync(item.DiceBearSeed);
        SaveToState();
    }

    public void CycleAvatar(AvatarAssignmentItem item) => RandomizeAvatar(item);

    public void CycleClassAvatars()
    {
        var rand = Random.Shared;
        int total = StandardPicturePasswordIcons.Length;

        foreach (var item in FilteredAvatarItems)
        {
            item.Avatar = Guid.NewGuid().ToString("N")[..10];
            int i1 = rand.Next(total);
            int i2 = rand.Next(total);
            while (i2 == i1) i2 = rand.Next(total);
            int i3 = rand.Next(total);
            while (i3 == i1 || i3 == i2) i3 = rand.Next(total);

            item.PicturePassword = [StandardPicturePasswordIcons[i1], StandardPicturePasswordIcons[i2], StandardPicturePasswordIcons[i3]];
            _ = DiceBearService.EnsureCachedAsync(item.DiceBearSeed);
        }

        SaveToState();
    }

    public void TogglePicturePasswordPolicy(bool isRequired)
    {
        PicturePasswordRequired = isRequired;
        _state.Config.PicturePasswordRequired = isRequired;

        if (!isRequired)
        {
            AuditLogger.RecordEntry(new AuditLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Event = "picture_password_policy_disabled",
                Outcome = "WARNING",
                SchoolCode = _state.School.Code,
                WindowsUser = Environment.UserName,
                Details = "Administrator disabled picture password requirement, re-introducing single-factor collision risk (B1)."
            });
        }
    }

    public void SaveToState()
    {
        _state.Config.PicturePasswordRequired = PicturePasswordRequired;
        foreach (var item in AvatarItems)
        {
            if (!string.IsNullOrWhiteSpace(item.StudentId))
            {
                _state.StudentAvatars[item.StudentId] = item.Avatar;
                _state.StudentPicturePasswords[item.StudentId] = item.PicturePassword;
            }
        }
    }

    public string GenerateAvatarSheetHtml(string? yearOrClassFilter = null, string? classFilter = null)
    {
        string targetYear;
        string targetClass;

        if (classFilter != null)
        {
            targetYear = string.IsNullOrWhiteSpace(yearOrClassFilter) ? (SelectedYearFilter ?? "Semua Tahun") : yearOrClassFilter;
            targetClass = classFilter;
        }
        else if (!string.IsNullOrWhiteSpace(yearOrClassFilter))
        {
            if (yearOrClassFilter.StartsWith("Tahun", StringComparison.OrdinalIgnoreCase) ||
                yearOrClassFilter.Equals("Semua Tahun", StringComparison.OrdinalIgnoreCase))
            {
                targetYear = yearOrClassFilter;
                targetClass = SelectedClassFilter ?? "Semua Kelas";
            }
            else
            {
                targetYear = "Semua Tahun";
                targetClass = yearOrClassFilter;
            }
        }
        else
        {
            targetYear = SelectedYearFilter ?? "Semua Tahun";
            targetClass = SelectedClassFilter ?? "Semua Kelas";
        }

        int filterGrade = ParseYearFilterToGrade(targetYear);
        bool isAllClasses = string.IsNullOrWhiteSpace(targetClass) || string.Equals(targetClass, "Semua Kelas", StringComparison.OrdinalIgnoreCase);

        var query = AvatarItems.AsEnumerable();
        if (filterGrade > 0)
        {
            query = query.Where(a => a.Grade == filterGrade);
        }

        if (!isAllClasses)
        {
            query = query.Where(a => string.Equals(a.ClassName, targetClass, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(a.FullClassDisplay, targetClass, StringComparison.OrdinalIgnoreCase));
        }

        var classGroups = query
            .GroupBy(a => (
                Grade: a.Grade,
                ClassName: a.ClassName ?? ""
            ))
            .OrderBy(g => g.Key.Grade)
            .ThenBy(g => g.Key.ClassName)
            .ToList();

        string pageTitleScope;
        if (filterGrade > 0)
        {
            pageTitleScope = isAllClasses
                ? $"Tahun {filterGrade} (Semua Kelas)"
                : $"Tahun {filterGrade} - {targetClass}";
        }
        else
        {
            pageTitleScope = isAllClasses
                ? "Semua Tahun & Kelas"
                : targetClass;
        }

        string pageTitle = $"Helaian Kata Laluan Gambar - {pageTitleScope} - {_state.School.Code}";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ms\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine($"  <title>{pageTitle}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif; }");
        sb.AppendLine("    body { background: #F8F9FA; color: #1E293B; padding: 24px; }");
        sb.AppendLine("    .container { max-width: 1020px; margin: 0 auto; }");
        sb.AppendLine("    .class-sheet { background: #FFFFFF; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); border: 1px solid #E2E8F0; margin-bottom: 32px; page-break-after: always; break-after: page; }");
        sb.AppendLine("    .class-sheet:last-child { margin-bottom: 0; page-break-after: auto; break-after: auto; }");
        sb.AppendLine("    .header { display: flex; align-items: center; justify-content: space-between; border-bottom: 3px solid #056839; padding-bottom: 18px; margin-bottom: 20px; }");
        sb.AppendLine("    .school-title { font-size: 22px; font-weight: 800; color: #056839; line-height: 1.2; }");
        sb.AppendLine("    .school-subtitle { font-size: 14px; color: #64748B; margin-top: 4px; font-weight: 600; }");
        sb.AppendLine("    .badges-group { display: flex; gap: 8px; align-items: center; }");
        sb.AppendLine("    .badge { background: #056839; color: #FFFFFF; padding: 6px 14px; border-radius: 20px; font-weight: 700; font-size: 14px; }");
        sb.AppendLine("    .year-badge { background: #0284C7; color: #FFFFFF; padding: 6px 12px; border-radius: 20px; font-weight: 700; font-size: 13px; }");
        sb.AppendLine("    .info-bar { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 10px; background: #F1F5F9; border-radius: 8px; padding: 12px 18px; margin-bottom: 24px; font-size: 13px; color: #334155; }");
        sb.AppendLine("    .info-bar strong { color: #0F172A; }");
        sb.AppendLine("    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; margin-bottom: 28px; }");
        sb.AppendLine("    .card { border: 1.5px solid #E2E8F0; border-radius: 10px; padding: 14px; background: #FFFFFF; display: flex; align-items: center; gap: 14px; page-break-inside: avoid; break-inside: avoid; }");
        sb.AppendLine("    .avatar-circle { width: 58px; height: 58px; border-radius: 50%; background: #F1F5F9; border: 2px solid #CBD5E1; overflow: hidden; flex-shrink: 0; display: flex; align-items: center; justify-content: center; }");
        sb.AppendLine("    .avatar-circle img { width: 100%; height: 100%; object-fit: cover; }");
        sb.AppendLine("    .student-info { overflow: hidden; flex: 1; }");
        sb.AppendLine("    .student-name { font-weight: 700; font-size: 14px; color: #0F172A; line-height: 1.3; margin-bottom: 2px; }");
        sb.AppendLine("    .student-id { font-size: 12px; color: #64748B; font-family: monospace; }");
        sb.AppendLine("    .meta-row { display: flex; gap: 6px; align-items: center; margin-top: 4px; flex-wrap: wrap; }");
        sb.AppendLine("    .avatar-tag { display: inline-block; background: #E0E7FF; color: #3730A3; font-weight: 700; font-size: 11px; padding: 2px 8px; border-radius: 12px; text-transform: capitalize; }");
        sb.AppendLine("    .pic-pw-tag { display: inline-flex; align-items: center; gap: 4px; background: #FEF3C7; color: #92400E; border: 1px solid #FCD34D; font-weight: 700; font-size: 11.5px; padding: 3px 8px; border-radius: 12px; margin-top: 4px; }");
        sb.AppendLine("    .footer { border-top: 1px solid #E2E8F0; padding-top: 14px; font-size: 12px; color: #64748B; line-height: 1.5; }");
        sb.AppendLine("    .footer strong { color: #334155; }");
        sb.AppendLine("    .no-print-bar { margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center; background: #FFFFFF; border: 1px solid #E2E8F0; padding: 12px 20px; border-radius: 10px; }");
        sb.AppendLine("    .print-summary { font-size: 14px; color: #334155; font-weight: 600; }");
        sb.AppendLine("    .btn-print { background: #056839; color: #FFFFFF; border: none; padding: 10px 22px; border-radius: 8px; font-weight: 700; font-size: 14px; cursor: pointer; display: inline-flex; align-items: center; gap: 8px; }");
        sb.AppendLine("    .btn-print:hover { background: #04522d; }");
        sb.AppendLine("    @media print {");
        sb.AppendLine("      body { background: #FFFFFF; padding: 0; }");
        sb.AppendLine("      .container { box-shadow: none; border: none; padding: 0; max-width: 100%; width: 100%; margin: 0; }");
        sb.AppendLine("      .class-sheet { box-shadow: none; border: none; padding: 0; margin: 0 0 0 0; page-break-after: always !important; break-after: page !important; page-break-inside: auto; break-inside: auto; }");
        sb.AppendLine("      .class-sheet:last-child { page-break-after: auto !important; break-after: auto !important; }");
        sb.AppendLine("      .header, .info-bar, .footer { page-break-inside: avoid !important; break-inside: avoid !important; }");
        sb.AppendLine("      .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; margin-bottom: 20px; }");
        sb.AppendLine("      .card { page-break-inside: avoid !important; break-inside: avoid !important; border: 1px solid #CBD5E1; padding: 10px; }");
        sb.AppendLine("      .no-print { display: none !important; }");
        sb.AppendLine("      @page { size: A4 portrait; margin: 12mm; }");
        sb.AppendLine("    }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <div class=\"no-print-bar no-print\">");
        sb.AppendLine($"      <div class=\"print-summary\">📋 Skop Cetakan: <strong>{pageTitleScope}</strong> ({classGroups.Sum(g => g.Count())} orang murid)</div>");
        sb.AppendLine($"      <button class=\"btn-print\" onclick=\"window.print()\">🖨️ Cetak Helaian Kata Laluan Gambar</button>");
        sb.AppendLine("    </div>");

        if (classGroups.Count == 0)
        {
            sb.AppendLine("    <div class=\"class-sheet\">");
            sb.AppendLine("      <p style=\"text-align:center; padding: 20px; color: #64748B;\">Tiada rekod murid dijumpai untuk skop yang dipilih.</p>");
            sb.AppendLine("    </div>");
        }
        else
        {
            foreach (var group in classGroups)
            {
                int sheetGrade = group.Key.Grade;
                string rawClass = group.Key.ClassName;
                var students = group.OrderBy(s => s.StudentName, StringComparer.OrdinalIgnoreCase).ToList();
                string sheetGradeLabel = sheetGrade > 0 ? $"Tahun {sheetGrade}" : "Tahun —";
                string displayClassTitle = group.First().FullClassDisplay;

                sb.AppendLine("    <div class=\"class-sheet\">");
                sb.AppendLine("      <div class=\"header\">");
                sb.AppendLine("        <div>");
                sb.AppendLine($"          <div class=\"school-title\">{_state.School.Name}</div>");
                sb.AppendLine($"          <div class=\"school-subtitle\">Helaian Avatar & Kata Laluan Gambar Murid (DELIMa SSO)</div>");
                sb.AppendLine("        </div>");
                sb.AppendLine("        <div class=\"badges-group\">");
                if (sheetGrade > 0)
                {
                    sb.AppendLine($"          <div class=\"year-badge\">{sheetGradeLabel}</div>");
                }
                sb.AppendLine($"          <div class=\"badge\">{_state.School.Code}</div>");
                sb.AppendLine("        </div>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"info-bar\">");
                sb.AppendLine($"        <div>Tahun: <strong>{sheetGradeLabel}</strong></div>");
                sb.AppendLine($"        <div>Kelas: <strong>{displayClassTitle}</strong></div>");
                sb.AppendLine($"        <div>Jumlah Murid: <strong>{students.Count} orang</strong></div>");
                sb.AppendLine($"        <div>Kata Laluan Gambar: <strong>{(PicturePasswordRequired ? "Aktif (3-Ikon)" : "Dinyahaktifkan")}</strong></div>");
                sb.AppendLine($"        <div>Tarikh: <strong>{DateTime.Now:dd/MM/yyyy}</strong></div>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"grid\">");

                foreach (var s in students)
                {
                    string avatarImgUrl = DiceBearService.GetAvatarUrl(s.DiceBearSeed);
                    sb.AppendLine("        <div class=\"card\">");
                    sb.AppendLine($"          <div class=\"avatar-circle\"><img src=\"{avatarImgUrl}\" alt=\"Avatar\" loading=\"lazy\" /></div>");
                    sb.AppendLine("          <div class=\"student-info\">");
                    sb.AppendLine($"            <div class=\"student-name\">{s.StudentName}</div>");
                    sb.AppendLine($"            <div class=\"student-id\">{(string.IsNullOrWhiteSpace(s.EmailLocal) ? s.StudentId : s.EmailLocal)}</div>");
                    sb.AppendLine("            <div class=\"meta-row\">");
                    sb.AppendLine($"              <span class=\"avatar-tag\">🏫 {s.FullClassDisplay}</span>");
                    sb.AppendLine("            </div>");
                    if (PicturePasswordRequired)
                    {
                        sb.AppendLine($"            <div class=\"pic-pw-tag\">🔑 {s.PicturePasswordDetailedDisplay}</div>");
                    }
                    sb.AppendLine("          </div>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"footer\">");
                sb.AppendLine("        <strong>Panduan Guru Kelas & Guru Penyelaras Makmal:</strong><br />");
                sb.AppendLine("        Helaian ini mengandungi simbol kata laluan gambar 3-ikon untuk murid log masuk secara pantas di PC Makmal Komputer. Simpan di meja makmal atau edarkan kepada guru kelas mengikut tahun.");
                sb.AppendLine("      </div>");
                sb.AppendLine("    </div>");
            }
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    window.onload = function() {");
        sb.AppendLine("      setTimeout(function() { window.print(); }, 400);");
        sb.AppendLine("    };");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    public string PrintAvatarSheet(string? yearOrClassFilter = null, string? classFilter = null)
    {
        string html = GenerateAvatarSheetHtml(yearOrClassFilter, classFilter);

        string targetYear = string.IsNullOrWhiteSpace(yearOrClassFilter) ? (SelectedYearFilter ?? "Semua_Tahun") : yearOrClassFilter;
        string targetClass = string.IsNullOrWhiteSpace(classFilter) ? (SelectedClassFilter ?? "Semua_Kelas") : classFilter;

        string sanitizedYear = string.Join("_", targetYear.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        string sanitizedClass = string.Join("_", targetClass.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");

        string fileName = $"Helaian_KataLaluanGambar_{sanitizedYear}_{sanitizedClass}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
        string exportDir = Path.Combine(Path.GetTempPath(), "Delima_Cetak");
        Directory.CreateDirectory(exportDir);
        string fullPath = Path.Combine(exportDir, fileName);

        File.WriteAllText(fullPath, html, Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });

        AuditLogger.RecordEntry(new AuditLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            Event = "avatar_sheet_printed",
            Outcome = "SUCCESS",
            SchoolCode = _state.School.Code,
            WindowsUser = Environment.UserName,
            Details = $"Student picture password sheet printed/exported for Year '{targetYear}', Class '{targetClass}' ({fullPath})."
        });

        return fullPath;
    }
}

