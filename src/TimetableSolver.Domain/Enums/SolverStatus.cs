namespace TimetableSolver.Domain.Enums;

/// <summary>
/// Solver outcome, decoupled from the OR-Tools CpSolverStatus enum so the Domain
/// project never has to reference Google.OrTools.
/// </summary>
public enum SolverStatus
{
    Unknown,
    Optimal,
    Feasible,
    Infeasible,
    ModelInvalid
}
