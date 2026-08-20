using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat;

public interface IConstraintRule
{
    string RuleId { get; }

    IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables);
}
