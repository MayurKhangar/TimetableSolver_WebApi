namespace TimetableSolver.Domain.Entities;

public sealed class Teacher
{
    public required string Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }

    public int? MaxPeriodsPerDay { get; init; }

    public int? MaxPeriodsPerWeek { get; init; }
}
