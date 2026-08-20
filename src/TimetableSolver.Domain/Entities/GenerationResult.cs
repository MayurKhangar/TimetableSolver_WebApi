using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Domain.Entities;

public sealed class GenerationResult
{
    public required SolverStatus Status { get; init; }
    public required bool Success { get; init; }
    public required long WallTimeMs { get; init; }
    public required int TimeLimitSeconds { get; init; }
    public double? ObjectiveValue { get; init; }

    public required IReadOnlyList<ScheduledLesson> Lessons { get; init; }
    public required IReadOnlyList<DataConflict> DataConflicts { get; init; }
    public required IReadOnlyList<string> SchedulingConflicts { get; init; }

    public required int SectionsTotal { get; init; }
    public required int SectionsScheduled { get; init; }
    public required int TeachersInvolved { get; init; }
}
