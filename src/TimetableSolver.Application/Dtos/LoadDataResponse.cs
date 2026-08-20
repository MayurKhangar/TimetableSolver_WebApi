namespace TimetableSolver.Application.Dtos;

public sealed record DataConflictDto(string Type, string? Section, string? Item, string Message);

public sealed record LoadDataSummaryDto(
    int SectionsTotal,
    int TeachersTotal,
    int CurriculumItemsTotal,
    int SchedulableCurriculumItemsTotal,
    int UnassignedPlaceholderCount,
    int ZeroWorkloadCount,
    int MissingAssignmentCount,
    int WeeklyTeachingCapacity);

public sealed record LoadDataResponse(
    bool Success,
    string DataSourceMode,
    LoadDataSummaryDto Summary,
    IReadOnlyList<DataConflictDto> DataConflicts);
