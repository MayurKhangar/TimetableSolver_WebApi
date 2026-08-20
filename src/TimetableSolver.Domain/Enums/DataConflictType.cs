namespace TimetableSolver.Domain.Enums;

/// <summary>
/// Classifies a data-quality problem discovered while normalizing the raw dataset,
/// before the solver ever runs. Reported back to the caller instead of being silently dropped.
/// </summary>
public enum DataConflictType
{
    UnassignedTeacher,
    ZeroWorkload,
    MissingAssignment,
    MissingCurriculum,
    AmbiguousAssignment,
    TeacherOverload
}
