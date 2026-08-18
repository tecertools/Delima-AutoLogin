using Delima.Core.Roster;
using Xunit;

namespace Delima.Core.Tests.Roster;

public class RosterModelTests
{
    [Fact]
    public void Student_GetFullEmail_CombinesLocalPartAndDomain()
    {
        var student = new Student
        {
            Id = "s_01",
            Name = "Aisyah Binti Ahmad",
            EmailLocal = "m-12345678"
        };

        Assert.Equal("m-12345678@moe-dl.edu.my", student.GetFullEmail("moe-dl.edu.my"));
    }

    [Fact]
    public void Student_GetFullEmail_LeavesAlreadyFullEmailIntact()
    {
        var student = new Student
        {
            Id = "s_02",
            Name = "Danial",
            EmailLocal = "m-87654321@moe-dl.edu.my"
        };

        Assert.Equal("m-87654321@moe-dl.edu.my", student.GetFullEmail("moe-dl.edu.my"));
    }

    [Fact]
    public void Student_MatchesSearch_FindsOnFullName_DisplayName_AndEmail()
    {
        var student = new Student
        {
            Id = "s_01",
            Name = "Muhammad Danial Bin Rahim",
            DisplayName = "Muhammad Danial R.",
            EmailLocal = "m-10293847"
        };

        // Matches full name (e.g. teacher searching by patronymic "Rahim")
        Assert.True(student.MatchesSearch("Rahim"));
        Assert.True(student.MatchesSearch("rahim"));

        // Matches calling/display name
        Assert.True(student.MatchesSearch("Danial"));
        Assert.True(student.MatchesSearch("Muhammad Danial R."));

        // Matches email prefix
        Assert.True(student.MatchesSearch("m-1029"));

        // Non-matching query
        Assert.False(student.MatchesSearch("Haziq"));
    }

    [Theory]
    [InlineData(30, 7, 179, 99, 19)]
    [InlineData(34, 7, 179, 99, 19)]
    [InlineData(38, 8, 156, 99, 16)]
    [InlineData(44, 9, 137, 99, 14)]
    public void GridCalculator_CalculatesExpectedGrid_ForClassSizes(
        int pupilCount,
        int expectedCols,
        int expectedCardW,
        int expectedCardH,
        int expectedChars)
    {
        var grid = GridCalculator.Calculate(pupilCount);

        Assert.Equal(expectedCols, grid.Columns);
        Assert.Equal(5, grid.Rows);
        Assert.Equal(expectedCardW, grid.CardWidthPx);
        Assert.Equal(expectedCardH, grid.CardHeightPx);
        Assert.Equal(expectedChars, grid.ApproximateCharsPerLine);
    }
}
