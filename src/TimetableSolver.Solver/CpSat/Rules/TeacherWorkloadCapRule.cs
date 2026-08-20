using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;


public sealed class TeacherWorkloadCapRule : IConstraintRule
{
    public string RuleId => "OPT-WORKLOAD";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        foreach (var teacherId in variables.TeacherIdsWithVariables)
        {
            if (!school.TeachersById.TryGetValue(teacherId, out var teacher))
            {
                continue;
            }

            if (teacher.MaxPeriodsPerWeek is { } maxWeek)
            {
                var weekVars = school.BellSchedule.WorkingDays
                    .SelectMany(day => school.BellSchedule.SlotsOn(day).SelectMany(slot => variables.ForTeacherSlot(teacherId, day, slot.Period)))
                    .Distinct()
                    .ToList();

                if (weekVars.Count > 0)
                {
                    model.Add(LinearExpr.Sum(weekVars) <= maxWeek);
                }
            }

            if (teacher.MaxPeriodsPerDay is { } maxDay)
            {
                foreach (var day in school.BellSchedule.WorkingDays)
                {
                    var dayVars = school.BellSchedule.SlotsOn(day)
                        .SelectMany(slot => variables.ForTeacherSlot(teacherId, day, slot.Period))
                        .Distinct()
                        .ToList();

                    if (dayVars.Count > 0)
                    {
                        model.Add(LinearExpr.Sum(dayVars) <= maxDay);
                    }
                }
            }
        }

        return Array.Empty<string>();
    }
}
