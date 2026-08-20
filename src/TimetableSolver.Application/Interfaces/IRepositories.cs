using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Application.Interfaces;

public sealed record RawSectionRecord(string Id, string DisplayName, string Grade, string SectionName, string CurriculumKey, int Capacity);

public interface ISectionRepository
{
    Task<IReadOnlyList<RawSectionRecord>> GetSectionsAsync(CancellationToken cancellationToken = default);
}

public interface IBellScheduleRepository
{
    Task<BellSchedule> GetBellScheduleAsync(CancellationToken cancellationToken = default);
}

public sealed record RawCurriculumRow(string CurriculumKey, string Name, Domain.Enums.CurriculumItemType Type, int PeriodsPerWeek, int PeriodsPerDay);

public interface ICurriculumRepository
{
    Task<IReadOnlyList<RawCurriculumRow>> GetCurriculumAsync(CancellationToken cancellationToken = default);
}

public sealed record RawAssignmentRow(string TeacherCode, string TeacherName, string ClassLabel, string ItemName, int PeriodsPerWeekForClass, bool IsUnassignedPlaceholder);

public interface ITeacherAssignmentRepository
{
    Task<IReadOnlyList<Teacher>> GetTeacherRosterAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RawAssignmentRow>> GetAssignmentRowsAsync(CancellationToken cancellationToken = default);
}

public interface ISampleSchoolRepository
{
    Task<SchoolModel> GetSampleSchoolAsync(CancellationToken cancellationToken = default);
}
