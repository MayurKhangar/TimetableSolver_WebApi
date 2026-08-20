namespace TimetableSolver.Application.Options;

public sealed class SolverOptions
{
    public const string SectionName = "Solver";

    public int TimeLimitSeconds { get; set; } = 120;
    public int Workers { get; set; } = 8;

    public bool EnableSoftObjective { get; set; } = true;
}
