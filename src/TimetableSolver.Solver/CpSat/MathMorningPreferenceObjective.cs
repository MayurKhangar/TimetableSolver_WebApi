using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Solver.CpSat;

public sealed class MathMorningPreferenceObjective
{
    private const int PreferredPeriodCeiling = 4;
    public string RuleId => "PR-MATH";

    public LinearExpr BuildPenaltyExpression(SchoolModel school, TimetableVariables variables, int penaltyWeight)
    {
        var terms = new List<BoolVar>();

        foreach (var section in school.SchedulableSections)
        {
            foreach (var item in section.Curriculum.Where(c => c.IsSchedulable && c.Name.Contains("Mathematics", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var day in school.BellSchedule.WorkingDays)
                {
                    var isMorningEligibleDay = day is SchoolDay.Monday or SchoolDay.Tuesday or SchoolDay.Wednesday or SchoolDay.Thursday;

                    foreach (var slot in school.BellSchedule.SlotsOn(day))
                    {
                        var isOutsidePreferredWindow = !isMorningEligibleDay || slot.Period > PreferredPeriodCeiling;
                        if (!isOutsidePreferredWindow)
                        {
                            continue;
                        }

                        var v = variables.TryGet(section.Id, item.Id, day, slot.Period);
                        if (v is not null)
                        {
                            terms.Add(v);
                        }
                    }
                }
            }
        }

        return terms.Count == 0
            ? LinearExpr.Constant(0)
            : LinearExpr.WeightedSum(terms, terms.Select(_ => (long)penaltyWeight).ToArray());
    }
}
