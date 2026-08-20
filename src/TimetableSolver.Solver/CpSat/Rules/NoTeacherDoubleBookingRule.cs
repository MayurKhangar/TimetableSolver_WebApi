using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class NoTeacherDoubleBookingRule : IConstraintRule
{
    public string RuleId => "H1";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        foreach (var teacherId in variables.TeacherIdsWithVariables)
        {
            foreach (var day in school.BellSchedule.WorkingDays)
            {
                foreach (var slot in school.BellSchedule.SlotsOn(day))
                {
                    var vars = variables.ForTeacherSlot(teacherId, day, slot.Period);
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
