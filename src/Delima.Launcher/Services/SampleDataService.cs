using Delima.Core.Crypto;
using Delima.Core.Roster;
using Delima.Core.Store;
using ClassInfo = Delima.Core.Roster.ClassInfo;

namespace Delima.Launcher.Services;

/// <summary>
/// Provides synthetic test and development school roster data matching SK Seksyen 24 Shah Alam.
/// No real pupil data is used.
/// </summary>
public static class SampleDataService
{
    public static School CreateSampleSchool()
    {
        return new School
        {
            Code = "SKS24",
            Name = "SK Seksyen 24 Shah Alam",
            Motto = "Berilmu Berdisiplin",
            Domain = "moe-dl.edu.my"
        };
    }

    public static ThemeInfo CreateSampleTheme()
    {
        return new ThemeInfo
        {
            Primary = "#056839",
            Accent = "#F7941D",
            ClassColours =
            [
                "#C41118", "#9E2B0E", "#A85200", "#8A6100",
                "#056839", "#2F6B12", "#0A6265", "#7A4A21"
            ]
        };
    }

    public static List<ClassInfo> CreateSampleClasses()
    {
        return
        [
            new() { Id = "1_cemerlang", Name = "1 Cemerlang", Grade = 1, ColourIndex = 0 },
            new() { Id = "1_gemilang", Name = "1 Gemilang", Grade = 1, ColourIndex = 1 },
            new() { Id = "1_terbilang", Name = "1 Terbilang", Grade = 1, ColourIndex = 2 },
            new() { Id = "2_cemerlang", Name = "2 Cemerlang", Grade = 2, ColourIndex = 0 },
            new() { Id = "2_gemilang", Name = "2 Gemilang", Grade = 2, ColourIndex = 1 },
            new() { Id = "2_terbilang", Name = "2 Terbilang", Grade = 2, ColourIndex = 2 },
            new() { Id = "2_harapan", Name = "2 Harapan", Grade = 2, ColourIndex = 3 },
            new() { Id = "3_cemerlang", Name = "3 Cemerlang", Grade = 3, ColourIndex = 0 },
            new() { Id = "3_gemilang", Name = "3 Gemilang", Grade = 3, ColourIndex = 1 },
            new() { Id = "4_cemerlang", Name = "4 Cemerlang", Grade = 4, ColourIndex = 0 },
            new() { Id = "5_cemerlang", Name = "5 Cemerlang", Grade = 5, ColourIndex = 0 },
            new() { Id = "6_cemerlang", Name = "6 Cemerlang", Grade = 6, ColourIndex = 0 }
        ];
    }

    public static List<Student> CreateSampleClassStudents(string classId = "2_cemerlang")
    {
        // 36 synthetic pupils covering Malay, Chinese, and Indian naming conventions + collision disambiguation
        var rawPupils = new (string id, string name, string avatar)[]
        {
            ("s_0001", "Nur Aishah Binti Ahmad", "kucing"),
            ("s_0002", "Danial Hakim Bin Rosli", "ikan"),
            ("s_0003", "Siti Nurhaliza Binti Osman", "bunga"),
            ("s_0004", "Tan Wei Ming", "kereta"),
            ("s_0005", "Arjun A/L Kumaran", "bintang"),
            ("s_0006", "Iman Qistina Binti Farhan", "epal"),
            ("s_0007", "Haziq Iskandar Bin Zulkifli", "penyu"),
            ("s_0008", "Zara Ameena Binti Razak", "kucing"),
            ("s_0009", "Muhammad Amirul Bin Azman", "belon"),
            ("s_0010", "Hana Sofea Binti Zakaria", "payung"),
            ("s_0011", "Ravi A/L Mogan", "layang"),
            ("s_0012", "Syafiq Danial Bin Razali", "ikan"),
            ("s_0013", "Nabila Huda Binti Shukri", "bunga"),
            ("s_0014", "Muhammad Aqil Bin Imran", "bintang"),
            ("s_0015", "Puteri Balqis Binti Mahadzir", "kucing"),
            ("s_0016", "Irfan Zafri Bin Hairul", "kereta"),
            ("s_0017", "Alia Maisarah Binti Salleh", "epal"),
            ("s_0018", "Zulkifli Bin Hassan", "penyu"),
            ("s_0019", "Hakim Luqman Bin Kamal", "belon"),
            ("s_0020", "Yusof Bin Khalid", "payung"),
            ("s_0021", "Adam Harith Bin Johari", "layang"),
            ("s_0022", "Lim Jia Wei", "kereta"),
            ("s_0023", "Kavitha A/P Suresh", "bintang"),
            ("s_0024", "Nur Aishah Binti Osman", "kucing"), // Disambiguation test: Nur Aishah O.
            ("s_0025", "Nur Aishah Binti Zakaria", "kucing"), // Disambiguation test: Nur Aishah Z.
            ("s_0026", "Wong Jun Hao", "ikan"),
            ("s_0027", "Priya A/P Shanmugam", "bunga"),
            ("s_0028", "Syakir Haiqal Bin Zainal", "kereta"),
            ("s_0029", "Farah Nadia Binti Munir", "epal"),
            ("s_0030", "Harith Irfan Bin Mansor", "belon"),
            ("s_0031", "Siti Sarah Binti Latif", "payung"),
            ("s_0032", "Darren Tan Chee Keong", "layang"),
            ("s_0033", "Thivya A/P Ramesh", "penyu"),
            ("s_0034", "Khairul Anuar Bin Yusoff", "kucing"),
            ("s_0035", "Mei Ling Tan", "ikan"),
            ("s_0036", "Muaz Rayyan Bin Taufiq", "bintang")
        };

        var students = new List<Student>();
        int count = 1;
        // Sample 3-icon picture passwords for testing
        var samplePasswords = new[]
        {
            new[] { "kucing", "bunga", "kereta" },
            new[] { "bola", "ikan", "bintang" },
            new[] { "epal", "payung", "belon" },
            new[] { "pokok", "rumah", "buku" }
        };

        foreach (var (id, name, avatar) in rawPupils)
        {
            var chosenPw = samplePasswords[(count - 1) % samplePasswords.Length];
            var picPw = PicturePasswordHasher.CreatePicturePassword(chosenPw, Argon2Parameters.FastTest);

            students.Add(new Student
            {
                Id = id,
                Name = name,
                ClassId = classId,
                EmailLocal = $"m-{12000000 + count}",
                Avatar = avatar,
                PasswordVersion = 1,
                PicturePassword = picPw,
                Active = true
            });
            count++;
        }

        // Apply display name calculation from Delima.Core
        var displayNames = DisplayNameCalculator.ComputeDisplayNames(students);
        foreach (var s in students)
        {
            if (displayNames.TryGetValue(s.Id, out var dn))
            {
                s.DisplayName = dn;
            }
        }

        return students;
    }
}
