namespace Delima.Core.Roster;

/// <summary>
/// Contract for roster data access.
/// </summary>
public interface IRosterStore
{
    Task<School> GetSchoolAsync();
    Task<IReadOnlyList<int>> ListYearsAsync();
    Task<IReadOnlyList<ClassInfo>> ListClassesAsync(int? year = null);
    Task<IReadOnlyList<Student>> ListStudentsAsync(string classId);
    Task<Student?> GetStudentAsync(string id);
    Task<DateTimeOffset> LastUpdatedAsync();
}
