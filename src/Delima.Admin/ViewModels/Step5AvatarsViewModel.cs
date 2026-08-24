using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Delima.Admin.Models;
using Delima.Core.Audit;
using Delima.Core.Services;

namespace Delima.Admin.ViewModels;

public sealed partial class AvatarAssignmentItem : ObservableObject
{
    public string StudentId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string EmailLocal { get; init; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvatarUrl))]
    private string _avatar = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PicturePasswordDisplay))]
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
    public ObservableCollection<string> ClassNames { get; } = [];

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

    partial void OnSelectedClassFilterChanged(string? value)
    {
        UpdateFilteredAvatarItems();
    }

    public void UpdateFilteredAvatarItems()
    {
        FilteredAvatarItems.Clear();
        var filter = SelectedClassFilter;

        IEnumerable<AvatarAssignmentItem> items = string.IsNullOrWhiteSpace(filter) || filter == "Semua Kelas"
            ? AvatarItems
            : AvatarItems.Where(a => string.Equals(a.ClassName, filter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in items)
        {
            FilteredAvatarItems.Add(item);
        }
    }

    public void InitializeAvatars()
    {
        AvatarItems.Clear();
        ClassNames.Clear();

        var classes = _state.RosterStudents.Select(s => s.ClassName).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
        foreach (var c in classes)
            ClassNames.Add(c);

        if (ClassNames.Count > 1)
        {
            ClassNames.Insert(0, "Semua Kelas");
        }

        // Group by class and assign unique avatars per class
        foreach (var group in _state.RosterStudents.GroupBy(s => s.ClassName))
        {
            int avatarIdx = 0;
            foreach (var student in group)
            {
                // Use the stored avatar only if it is a new-style seed (not a legacy animal key).
                // Legacy keys & blank values are left empty so DiceBearSeed auto-resolves to student.Id.
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
                    EmailLocal = student.EmailLocal,
                    Avatar = avatar,
                    PicturePassword = picPassword
                });
            }
        }

        if (ClassNames.Count > 0)
        {
            SelectedClassFilter = ClassNames.Count > 1 ? ClassNames[1] : ClassNames[0];
        }
        else
        {
            UpdateFilteredAvatarItems();
        }

        // Pre-cache all avatar PNGs in the background (best-effort, non-blocking)
        var seeds = AvatarItems.Select(a => a.DiceBearSeed).ToList();
        _ = DiceBearService.PreCacheBatchAsync(seeds);
    }

    /// <summary>
    /// Assigns a new random DiceBear seed to the student, giving them a different critter.
    /// </summary>
    public void RandomizeAvatar(AvatarAssignmentItem item)
    {
        item.Avatar = Guid.NewGuid().ToString("N")[..10];
        _ = DiceBearService.EnsureCachedAsync(item.DiceBearSeed);
        SaveToState();
    }

    // Kept for backward compatibility with any remaining callers
    public void CycleAvatar(AvatarAssignmentItem item) => RandomizeAvatar(item);

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

    public string GenerateAvatarSheetHtml(string? className = null)
    {
        string targetFilter = string.IsNullOrWhiteSpace(className)
            ? (SelectedClassFilter ?? "Semua Kelas")
            : className;

        bool isAllClasses = string.Equals(targetFilter, "Semua Kelas", StringComparison.OrdinalIgnoreCase);

        var classGroups = isAllClasses
            ? AvatarItems.GroupBy(a => a.ClassName).OrderBy(g => g.Key).ToList()
            : AvatarItems.Where(a => string.Equals(a.ClassName, targetFilter, StringComparison.OrdinalIgnoreCase))
                         .GroupBy(a => a.ClassName).ToList();

        string pageTitle = isAllClasses
            ? $"Helaian Avatar Semua Kelas - {_state.School.Code}"
            : $"Helaian Avatar Kelas - {targetFilter} - {_state.School.Code}";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"ms\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine($"  <title>{pageTitle}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif; }");
        sb.AppendLine("    body { background: #F8F9FA; color: #1E293B; padding: 24px; }");
        sb.AppendLine("    .container { max-width: 960px; margin: 0 auto; }");
        sb.AppendLine("    .class-sheet { background: #FFFFFF; border-radius: 12px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); border: 1px solid #E2E8F0; margin-bottom: 32px; page-break-after: always; break-after: page; }");
        sb.AppendLine("    .class-sheet:last-child { margin-bottom: 0; page-break-after: auto; break-after: auto; }");
        sb.AppendLine("    .header { display: flex; align-items: center; justify-content: space-between; border-bottom: 3px solid #056839; padding-bottom: 20px; margin-bottom: 24px; }");
        sb.AppendLine("    .school-title { font-size: 22px; font-weight: 800; color: #056839; }");
        sb.AppendLine("    .school-subtitle { font-size: 14px; color: #64748B; margin-top: 4px; }");
        sb.AppendLine("    .badge { background: #056839; color: #FFFFFF; padding: 6px 14px; border-radius: 20px; font-weight: 700; font-size: 14px; }");
        sb.AppendLine("    .info-bar { display: flex; justify-content: space-between; background: #F1F5F9; border-radius: 8px; padding: 12px 18px; margin-bottom: 24px; font-size: 13px; color: #334155; }");
        sb.AppendLine("    .info-bar strong { color: #0F172A; }");
        sb.AppendLine("    .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 16px; margin-bottom: 32px; }");
        sb.AppendLine("    .card { border: 1px solid #E2E8F0; border-radius: 10px; padding: 14px; background: #FFFFFF; display: flex; align-items: center; gap: 14px; page-break-inside: avoid; break-inside: avoid; }");
        sb.AppendLine("    .avatar-circle { width: 56px; height: 56px; border-radius: 50%; background: #F1F5F9; border: 2px solid #CBD5E1; overflow: hidden; flex-shrink: 0; display: flex; align-items: center; justify-content: center; }");
        sb.AppendLine("    .avatar-circle img { width: 100%; height: 100%; object-fit: cover; }");
        sb.AppendLine("    .student-info { overflow: hidden; }");
        sb.AppendLine("    .student-name { font-weight: 700; font-size: 14px; color: #0F172A; line-height: 1.3; margin-bottom: 2px; }");
        sb.AppendLine("    .student-id { font-size: 12px; color: #64748B; font-family: monospace; }");
        sb.AppendLine("    .avatar-tag { display: inline-block; background: #E0E7FF; color: #3730A3; font-weight: 700; font-size: 11px; padding: 2px 8px; border-radius: 12px; margin-top: 4px; text-transform: capitalize; }");
        sb.AppendLine("    .pic-pw-tag { display: inline-block; background: #FEF3C7; color: #92400E; border: 1px solid #FCD34D; font-weight: 600; font-size: 11px; padding: 2px 8px; border-radius: 12px; margin-top: 4px; }");
        sb.AppendLine("    .footer { border-top: 1px solid #E2E8F0; padding-top: 16px; font-size: 12px; color: #64748B; line-height: 1.5; }");
        sb.AppendLine("    .footer strong { color: #334155; }");
        sb.AppendLine("    .no-print-bar { margin-bottom: 20px; display: flex; justify-content: flex-end; gap: 10px; }");
        sb.AppendLine("    .btn-print { background: #056839; color: #FFFFFF; border: none; padding: 10px 20px; border-radius: 8px; font-weight: 700; font-size: 14px; cursor: pointer; }");
        sb.AppendLine("    .btn-print:hover { background: #04522d; }");
        sb.AppendLine("    @media print {");
        sb.AppendLine("      body { background: #FFFFFF; padding: 0; }");
        sb.AppendLine("      .container { box-shadow: none; border: none; padding: 0; max-width: 100%; }");
        sb.AppendLine("      .class-sheet { box-shadow: none; border: none; padding: 0; margin-bottom: 0; page-break-after: always; break-after: page; }");
        sb.AppendLine("      .class-sheet:last-child { page-break-after: auto; break-after: auto; }");
        sb.AppendLine("      .no-print { display: none !important; }");
        sb.AppendLine("      @page { size: A4 portrait; margin: 12mm; }");
        sb.AppendLine("    }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <div class=\"no-print-bar no-print\">");
        sb.AppendLine($"      <button class=\"btn-print\" onclick=\"window.print()\">🖨️ Cetak Helaian Avatar {(isAllClasses ? "Semua Kelas" : targetFilter)}</button>");
        sb.AppendLine("    </div>");

        if (classGroups.Count == 0)
        {
            sb.AppendLine("    <div class=\"class-sheet\">");
            sb.AppendLine("      <p style=\"text-align:center; padding: 20px; color: #64748B;\">Tiada murid dijumpai.</p>");
            sb.AppendLine("    </div>");
        }
        else
        {
            foreach (var group in classGroups)
            {
                string currentClass = string.IsNullOrWhiteSpace(group.Key) ? (isAllClasses ? "Tanpa Kelas" : targetFilter) : group.Key;
                var students = group.ToList();

                sb.AppendLine("    <div class=\"class-sheet\">");
                sb.AppendLine("      <div class=\"header\">");
                sb.AppendLine("        <div>");
                sb.AppendLine($"          <div class=\"school-title\">{_state.School.Name}</div>");
                sb.AppendLine($"          <div class=\"school-subtitle\">Helaian Avatar & Log Masuk DELIMa Murid</div>");
                sb.AppendLine("        </div>");
                sb.AppendLine($"        <div class=\"badge\">{_state.School.Code}</div>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"info-bar\">");
                sb.AppendLine($"        <div>Kelas: <strong>{currentClass}</strong></div>");
                sb.AppendLine($"        <div>Jumlah Murid: <strong>{students.Count}</strong></div>");
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
                    sb.AppendLine($"            <div class=\"avatar-tag\">🐾 {s.ClassName}</div>");
                    if (PicturePasswordRequired)
                    {
                        sb.AppendLine($"            <div class=\"pic-pw-tag\">🔑 {s.PicturePasswordDisplay}</div>");
                    }
                    sb.AppendLine("          </div>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"footer\">");
                sb.AppendLine("        <strong>Panduan Guru Kelas & Guru Makmal:</strong><br />");
                sb.AppendLine("        Helaian ini boleh dipamerkan pada sudut kenyataan kelas atau disimpan di meja makmal komputer untuk membantu murid Tahun 1 & 2 mengenal pasti akaun masing-masing.");
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

    public string PrintAvatarSheet(string? className = null)
    {
        string targetClass = string.IsNullOrWhiteSpace(className)
            ? (SelectedClassFilter ?? (ClassNames.FirstOrDefault() ?? "Kelas"))
            : className;

        string html = GenerateAvatarSheetHtml(targetClass);
        string sanitizedClass = string.Join("_", targetClass.Split(Path.GetInvalidFileNameChars()));
        string fileName = $"Helaian_Avatar_{sanitizedClass}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
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
            Details = $"Class avatar sheet printed/exported for class '{targetClass}' ({fullPath})."
        });

        return fullPath;
    }
}
