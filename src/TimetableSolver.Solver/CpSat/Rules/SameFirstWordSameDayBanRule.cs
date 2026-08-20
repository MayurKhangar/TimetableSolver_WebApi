using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class SameFirstWordSameDayBanRule : IConstraintRule
{
    public string RuleId => "L1";

    /// <summary>
    /// Subjects sharing the same first word (e.g. "English Language" / "English Literature") may not be
    /// scheduled on the same day. This is HARD per scheduling-rules.json (L1 was moved from soft to
    /// mandatoryHard in production). Enforced strictly wherever it is mathematically satisfiable.
    ///
    /// A first-word group is only satisfiable if the group's items can be spread across enough distinct
    /// days: each item needs at least ceil(periodsPerWeek / maxPeriodsPerDay) exclusive days, and since
    /// the rule forbids sharing a day, those day-sets must be disjoint. When the sum of that minimum
    /// across a group exceeds the number of working days (documented as a known rule/data conflict in
    /// TIMETABLE_GENERATION_DATA_REQUIREMENTS.md §8.3 — e.g. Class 12 "English Language" + "English
    /// Literature" need 7 days but only 6 exist), strict enforcement makes the whole model INFEASIBLE.
    /// In that case we allow only the unavoidable minimum number of same-day overlaps (mirroring the
    /// production engine's own force-fill Pass A2, which relaxes L1 as a last resort) and report exactly
    /// which section/group/day-count forced the relaxation.
    /// </summary>
    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        var notes = new List<string>();
        var workingDays = school.BellSchedule.WorkingDays;

        foreach (var section in school.SchedulableSections)
        {
            var groups = section.Curriculum
                .Where(c => c.IsSchedulable)
                .GroupBy(c => c.FirstWord, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                var groupItems = group.ToList();
                var minDaysNeeded = groupItems.Sum(item => CeilDiv(item.PeriodsPerWeek, item.MaxPeriodsPerDay));
                var overflow = Math.Max(0, minDaysNeeded - workingDays.Count);

                var violationVars = new List<BoolVar>();

                foreach (var day in workingDays)
                {
                    var usedIndicators = new List<BoolVar>();

                    foreach (var item in groupItems)
                    {
                        var dayVars = variables.ForSectionItemDay(section.Id, item.Id, day);
                        if (dayVars.Count == 0)
                        {
                            continue;
                        }

                        var usedToday = model.NewBoolVar($"used[{section.Id}|{item.Id}|{day}]");
                        model.AddMaxEquality(usedToday, dayVars);
                        usedIndicators.Add(usedToday);
                    }

                    if (usedIndicators.Count <= 1)
                    {
                        continue;
                    }

                    if (overflow == 0)
                    {
                        model.AddAtMostOne(usedIndicators);
                        continue;
                    }

                    // Allow this day to break the rule only when the group's own violation budget is used.
                    var violatesToday = model.NewBoolVar($"L1violation[{section.Id}|{group.Key}|{day}]");
                    model.Add(LinearExpr.Sum(usedIndicators) <= 1 + (violatesToday * (usedIndicators.Count - 1)));
                    violationVars.Add(violatesToday);
                }

                if (overflow > 0)
                {
                    if (violationVars.Count > 0)
                    {
                        model.Add(LinearExpr.Sum(violationVars) <= overflow);
                    }

                    var breakdown = string.Join(" + ", groupItems.Select(item =>
                        $"{item.Name} ({item.PeriodsPerWeek} ppw, max {item.MaxPeriodsPerDay}/day → ≥{CeilDiv(item.PeriodsPerWeek, item.MaxPeriodsPerDay)} days)"));

                    notes.Add(
                        $"L1 relaxed for {section.DisplayName}, group \"{group.Key}\": {breakdown} " +
                        $"together need at least {minDaysNeeded} distinct days but only {workingDays.Count} working days exist " +
                        $"(known data/rule conflict — TIMETABLE_GENERATION_DATA_REQUIREMENTS.md §8.3). " +
                        $"Allowed the minimum unavoidable {overflow} day(s) of overlap.");
                }
            }
        }

        return notes;
    }

    private static int CeilDiv(int numerator, int denominator) =>
        denominator <= 0 ? numerator : (numerator + denominator - 1) / denominator;
}
