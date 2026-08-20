using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class DailySubjectMaxRule : IConstraintRule
{
    public string RuleId => "H7";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        foreach (var section in school.SchedulableSections)
        {
            foreach (var item in section.Curriculum.Where(c => c.IsSchedulable))
            {
                foreach (var day in school.BellSchedule.WorkingDays)
                {
                    var vars = variables.ForSectionItemDay(section.Id, item.Id, day);
                    if (vars.Count == 0)
                    {
                        continue;
                    }

                    model.Add(LinearExpr.Sum(vars) <= item.MaxPeriodsPerDay);
                }
            }
        }

        return Array.Empty<string>();
    }
}
