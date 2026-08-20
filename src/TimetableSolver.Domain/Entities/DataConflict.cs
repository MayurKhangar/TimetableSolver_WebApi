using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Domain.Entities;

public sealed class DataConflict
{
    public required DataConflictType Type { get; init; }
    public string? SectionId { get; init; }
    public string? SectionDisplayName { get; init; }
    public string? ItemName { get; init; }
    public required string Message { get; init; }
}
