using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class OneLessonPerSectionSlotRule : IConstraintRule
{
    public string RuleId => "H3";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        foreach (var section in school.SchedulableSections)
        {
            foreach (var day in school.BellSchedule.WorkingDays)
            {
                foreach (var slot in school.BellSchedule.SlotsOn(day))
                {
                    var vars = variables.ForSectionSlot(section.Id, day, slot.Period);
                    if (vars.Count > 1)
                    {
                        model.AddAtMostOne(vars);
                    }
                }
            }
        }

        return Array.Empty<string>();
    }
}
