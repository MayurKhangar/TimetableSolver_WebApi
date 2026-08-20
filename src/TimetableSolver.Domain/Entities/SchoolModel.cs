namespace TimetableSolver.Domain.Entities;

public sealed class SchoolModel
{
    public required string AcademicYear { get; init; }
    public required BellSchedule BellSchedule { get; init; }
    public required IReadOnlyList<Section> Sections { get; init; }
    public required IReadOnlyDictionary<string, Teacher> TeachersById { get; init; }
    public required IReadOnlyList<DataConflict> DataConflicts { get; init; }

    public IEnumerable<Section> SchedulableSections => Sections.Where(s => s.Curriculum.Any(c => c.IsSchedulable));
}
