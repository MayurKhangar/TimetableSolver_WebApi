namespace TimetableSolver.Application.Dtos;

public sealed record SolverInfoDto(string Engine, string Status, long WallTimeMs, int TimeLimitSeconds, double? ObjectiveValue);

public sealed record GenerationSummaryDto(
    int SectionsTotal,
    int SectionsScheduled,
    int TotalSlotsScheduled,
    int TeachersInvolved,
    int UnassignedPlaceholderRows,
    int ZeroWorkloadRows);

public sealed record LessonDto(int Period, string Subject, string TeacherCode, string TeacherName);

public sealed record SectionLessonDto(int Period, string Section, string Subject);

public sealed record GenerateResponse(
    bool Success,
    SolverInfoDto Solver,
    GenerationSummaryDto? Summary,
    IReadOnlyList<DataConflictDto> DataConflicts,
    IReadOnlyList<string> SchedulingConflicts,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<LessonDto>>>? SectionTimetables,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>>>? TeacherTimetables,
    ValidationDto? Validation);

public sealed record ValidationDto(bool HardConstraintsSatisfied, double SoftConstraintPenalty, IReadOnlyList<string> ViolatedSoftConstraints);
