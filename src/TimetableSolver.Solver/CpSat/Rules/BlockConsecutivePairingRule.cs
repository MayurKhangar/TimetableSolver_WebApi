using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class BlockConsecutivePairingRule : IConstraintRule
{
    public string RuleId => "H5+B1+L2";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        foreach (var section in school.SchedulableSections)
        {
            foreach (var item in section.Curriculum.Where(c => c.IsSchedulable && c.MaxPeriodsPerDay >= 2))
            {
                foreach (var day in school.BellSchedule.WorkingDays)
                {
                    var slotsToday = school.BellSchedule.SlotsOn(day).OrderBy(s => s.Period).ToList();

                    for (var i = 0; i < slotsToday.Count; i++)
                    {
                        for (var j = i + 1; j < slotsToday.Count; j++)
                        {
                            var a = slotsToday[i];
                            var b = slotsToday[j];

                            // Two adjacent slots in the same pair-group are a legitimate block — allowed together.
                            var isAdjacentPair = a.PairGroup == b.PairGroup && b.Period == a.Period + 1;
                            if (isAdjacentPair)
                            {
                                continue;
                            }

                            var varA = variables.TryGet(section.Id, item.Id, day, a.Period);
                            var varB = variables.TryGet(section.Id, item.Id, day, b.Period);

                            if (varA is not null && varB is not null)
                            {
                                model.Add(varA + varB <= 1);
                            }
                        }
                    }
                }
            }
        }

        return Array.Empty<string>();
    }
}
