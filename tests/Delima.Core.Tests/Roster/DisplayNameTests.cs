using Delima.Core.Roster;
using Xunit;

namespace Delima.Core.Tests.Roster;

public class DisplayNameTests
{
    public static readonly TheoryData<string, string[], string[]> NamingConventionFixtures = new()
    {
        // 1. Malay Patronymics (bin, binti, bt, bte)
        { "Muhammad Danial Bin Rahim", ["Muhammad", "Danial"], ["Rahim"] },
        { "Nur Aishah Binti Ahmad", ["Nur", "Aishah"], ["Ahmad"] },
        { "Siti Nurhaliza Bt Tarudin", ["Siti", "Nurhaliza"], ["Tarudin"] },
        { "Farah Bte Yusof", ["Farah"], ["Yusof"] },
        { "Muhammad Haziq bin Osman", ["Muhammad", "Haziq"], ["Osman"] },
        { "Wan Nurul Ain binti Wan Zulkifli", ["Wan", "Nurul", "Ain"], ["Wan", "Zulkifli"] },

        // 2. Indian Patronymics (a/l, a/p, s/o, d/o)
        { "Arjun A/L Kumaran", ["Arjun"], ["Kumaran"] },
        { "Priya A/P Selvam", ["Priya"], ["Selvam"] },
        { "Ravi S/O Mohan", ["Ravi"], ["Mohan"] },
        { "Kavitha D/O Rajan", ["Kavitha"], ["Rajan"] },
        { "Karthik a/l Ramanathan", ["Karthik"], ["Ramanathan"] },

        // 3. Chinese Names (Surname first, 3+ words)
        { "Tan Wei Ming", ["Wei", "Ming"], ["Tan"] },
        { "Chong Mei Ling", ["Mei", "Ling"], ["Chong"] },
        { "Lee Jia Jun", ["Jia", "Jun"], ["Lee"] },
        { "Ng Kai Xuan", ["Kai", "Xuan"], ["Ng"] },
        { "Lim Xiao Xuan Mary", ["Xiao", "Xuan", "Mary"], ["Lim"] },

        // 4. East Malaysian / Indigenous (anak / ak)
        { "Dayang Anak Libau", ["Dayang"], ["Libau"] },
        { "Jimbau ak Unchat", ["Jimbau"], ["Unchat"] },

        // 5. Short / Non-particle names (1-2 words)
        { "Adam Daniel", ["Adam", "Daniel"], [] },
        { "Aisyah", ["Aisyah"], [] },
        { "Sarah Smith", ["Sarah", "Smith"], [] }
    };

    [Theory]
    [MemberData(nameof(NamingConventionFixtures))]
    public void NameSplitter_SplitsCorrectly_AcrossConventions(string inputName, string[] expectedGiven, string[] expectedRest)
    {
        var parsed = NameSplitter.Split(inputName);

        Assert.Equal(expectedGiven, parsed.Given);
        Assert.Equal(expectedRest, parsed.Rest);
    }

    [Fact]
    public void DisplayNameCalculator_ComputesDisambiguatedFloor_ForMixedClass()
    {
        // Reference class from Normal_SSO §4.3
        var students = new List<Student>
        {
            new() { Id = "1", Name = "Muhammad Danial Bin Rahim" },
            new() { Id = "2", Name = "Muhammad Danial Bin Salleh" },
            new() { Id = "3", Name = "Muhammad Amirul Bin Zaki" },
            new() { Id = "4", Name = "Nur Aishah Binti Ahmad" },
            new() { Id = "5", Name = "Nur Aishah Binti Osman" },
            new() { Id = "6", Name = "Tan Wei Ming" },
            new() { Id = "7", Name = "Lee Wei Ming" },
            new() { Id = "8", Name = "Chong Mei Ling" },
            new() { Id = "9", Name = "Arjun A/L Kumaran" },
            new() { Id = "10", Name = "Arjun A/L Selvam" }
        };

        var displayNames = DisplayNameCalculator.ComputeDisplayNames(students);

        Assert.Equal("Muhammad Danial R.", displayNames["1"]);
        Assert.Equal("Muhammad Danial S.", displayNames["2"]);
        Assert.Equal("Muhammad Amirul", displayNames["3"]); // No collision in calling name
        Assert.Equal("Nur Aishah A.", displayNames["4"]);
        Assert.Equal("Nur Aishah O.", displayNames["5"]);
        Assert.Equal("Wei Ming T.", displayNames["6"]); // Disambiguated with surname initial
        Assert.Equal("Wei Ming L.", displayNames["7"]); // Disambiguated with surname initial
        Assert.Equal("Mei Ling", displayNames["8"]); // No collision
        Assert.Equal("Arjun K.", displayNames["9"]);
        Assert.Equal("Arjun S.", displayNames["10"]);
    }

    public static readonly TheoryData<string, int, string> CardWidthFixtures = new()
    {
        // 7-column card (width = 179px, ~19 chars/line, fits up to 38 chars over 2 lines)
        { "Muhammad Danial Bin Rahim", 179, "Muhammad Danial Bin Rahim" },
        { "Nur Aishah Binti Ahmad", 179, "Nur Aishah Binti Ahmad" },
        { "Tan Wei Ming", 179, "Tan Wei Ming" },

        // 9-column card (width = 137px, ~14 chars/line, tight - falls back to calling name)
        { "Muhammad Danial Bin Rahim", 137, "Muhammad Danial R." },
        { "Nur Aishah Binti Ahmad", 137, "Nur Aishah A." },
        { "Tan Wei Ming", 137, "Wei Ming T." }
    };

    [Theory]
    [MemberData(nameof(CardWidthFixtures))]
    public void DisplayNameCalculator_ComputesAdaptiveName_AtDifferentCardWidths(
        string studentName,
        int cardWidth,
        string expectedDisplay)
    {
        var classStudents = new List<Student>
        {
            new() { Id = "1", Name = "Muhammad Danial Bin Rahim" },
            new() { Id = "2", Name = "Muhammad Danial Bin Salleh" },
            new() { Id = "3", Name = "Nur Aishah Binti Ahmad" },
            new() { Id = "4", Name = "Nur Aishah Binti Osman" },
            new() { Id = "5", Name = "Tan Wei Ming" },
            new() { Id = "6", Name = "Lee Wei Ming" }
        };

        var target = classStudents.First(s => s.Name == studentName);
        string actual = DisplayNameCalculator.ComputeAdaptiveDisplayName(target, classStudents, cardWidth);

        Assert.Equal(expectedDisplay, actual);
    }
}
