namespace TimetableSolver.Domain.Entities;

public sealed class Section
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Grade { get; init; }
    public required string SectionName { get; init; }
    public required string CurriculumKey { get; init; }
    public int Capacity { get; init; }

    public List<CurriculumItem> Curriculum { get; init; } = new();
}
