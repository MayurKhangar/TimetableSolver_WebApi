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

    public string? TeacherId { get; init; }

    public bool IsUnassignedPlaceholder { get; init; }

    public bool IsSchedulable => !IsUnassignedPlaceholder && PeriodsPerWeek > 0 && !string.IsNullOrWhiteSpace(TeacherId);

    public string FirstWord => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
        ? parts[0]
        : Name;
}

