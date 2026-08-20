using TimetableSolver.Application.Dtos;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Application.Mapping;

public static class TimetableMapper
{
    public static DataConflictDto ToDto(this DataConflict conflict) =>
        new(conflict.Type.ToString(), conflict.SectionDisplayName ?? conflict.SectionId, conflict.ItemName, conflict.Message);

    public static LoadDataResponse ToLoadDataResponse(this SchoolModel school, string dataSourceMode)
    {
        var allItems = school.Sections.SelectMany(s => s.Curriculum).ToList();

        var summary = new LoadDataSummaryDto(
            SectionsTotal: school.Sections.Count,
            TeachersTotal: school.TeachersById.Count,
            CurriculumItemsTotal: allItems.Count,
            SchedulableCurriculumItemsTotal: allItems.Count(c => c.IsSchedulable),
            UnassignedPlaceholderCount: school.DataConflicts.Count(c => c.Type == DataConflictType.UnassignedTeacher),
            ZeroWorkloadCount: school.DataConflicts.Count(c => c.Type == DataConflictType.ZeroWorkload),
            MissingAssignmentCount: school.DataConflicts.Count(c => c.Type == DataConflictType.MissingAssignment),
            WeeklyTeachingCapacity: school.BellSchedule.WeeklyTeachingCapacity);

        return new LoadDataResponse(
            Success: true,
            DataSourceMode: dataSourceMode,
            Summary: summary,
            DataConflicts: school.DataConflicts.Select(c => c.ToDto()).ToList());
    }

    public static GenerateResponse ToGenerateResponse(this GenerationResult result)
    {
        var solverDto = new SolverInfoDto("Google OR-Tools CP-SAT", result.Status.ToString(), result.WallTimeMs, result.TimeLimitSeconds, result.ObjectiveValue);

        if (!result.Success)
        {
            return new GenerateResponse(
                Success: false,
                Solver: solverDto,
                Summary: null,
                DataConflicts: result.DataConflicts.Select(c => c.ToDto()).ToList(),
                SchedulingConflicts: result.SchedulingConflicts,
                SectionTimetables: null,
                TeacherTimetables: null,
                Validation: null);
        }

        var summary = new GenerationSummaryDto(SectionsTotal: result.SectionsTotal, SectionsScheduled: result.SectionsScheduled,TotalSlotsScheduled: result.Lessons.Count,
            TeachersInvolved: result.TeachersInvolved, UnassignedPlaceholderRows: result.DataConflicts.Count(c => c.Type == DataConflictType.UnassignedTeacher),
            ZeroWorkloadRows: result.DataConflicts.Count(c => c.Type == DataConflictType.ZeroWorkload));

        var sectionTimetables = result.Lessons
            .GroupBy(l => l.SectionDisplayName)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, IReadOnlyList<LessonDto>>)g
                    .GroupBy(l => l.Day.ToString().ToUpperInvariant())
                    .ToDictionary(
                        dg => dg.Key,
                        dg => (IReadOnlyList<LessonDto>)dg.OrderBy(l => l.Period)
                            .Select(l => new LessonDto(l.Period, l.ItemName, l.TeacherCode, l.TeacherName))
                            .ToList()));

        var teacherTimetables = result.Lessons
            .GroupBy(l => l.TeacherName)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>>)g
                    .GroupBy(l => l.Day.ToString().ToUpperInvariant())
                    .ToDictionary(
                        dg => dg.Key,
                        dg => (IReadOnlyList<SectionLessonDto>)dg.OrderBy(l => l.Period)
                            .Select(l => new SectionLessonDto(l.Period, l.SectionDisplayName, l.ItemName))
                            .ToList()));

        var validation = new ValidationDto(
            HardConstraintsSatisfied: true, 
            SoftConstraintPenalty: result.ObjectiveValue ?? 0,
            ViolatedSoftConstraints: Array.Empty<string>());

        return new GenerateResponse(
            Success: true,
            Solver: solverDto,
            Summary: summary,
            DataConflicts: result.DataConflicts.Select(c => c.ToDto()).ToList(),
            SchedulingConflicts: result.SchedulingConflicts,
            SectionTimetables: sectionTimetables,
            TeacherTimetables: teacherTimetables,
            Validation: validation);
    }
}
