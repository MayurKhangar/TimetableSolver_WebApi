using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class WeeklyCurriculumTotalsRule : IConstraintRule
{
    public string RuleId => "H8";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        foreach (var section in school.SchedulableSections)
        {
            foreach (var item in section.Curriculum.Where(c => c.IsSchedulable))
            {
                var vars = variables.ForSectionItem(section.Id, item.Id);
                if (vars.Count == 0)
                {
                    continue;
                }

                model.Add(LinearExpr.Sum(vars) == item.PeriodsPerWeek);
            }
        }

        return Array.Empty<string>();
    }
}
