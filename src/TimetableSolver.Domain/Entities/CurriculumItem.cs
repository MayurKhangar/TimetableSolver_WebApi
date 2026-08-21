using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Domain.Entities;

public sealed class CurriculumItem
{
    public required string Id { get; init; }

    public required string SectionId { get; init; }
    public required string Name { get; init; }
    public required CurriculumItemType Type { get; init; }
    public required int PeriodsPerWeek { get; init; }
    public required int MaxPeriodsPerDay { get; init; }

    /// <summary>
    /// Settable (not <c>init</c>) because a post-load reconciliation step may clear it after construction,
    /// when a teacher's aggregate class-level assignment exceeds weekly capacity and this specific item is
    /// the one excluded to bring that teacher back under capacity (see `TeacherOverloadReconciler`).
    /// </summary>
    public string? TeacherId { get; set; }

    public bool IsUnassignedPlaceholder { get; init; }

    public bool IsSchedulable => !IsUnassignedPlaceholder && PeriodsPerWeek > 0 && !string.IsNullOrWhiteSpace(TeacherId);

    public string FirstWord => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
        ? parts[0]
        : Name;
}

