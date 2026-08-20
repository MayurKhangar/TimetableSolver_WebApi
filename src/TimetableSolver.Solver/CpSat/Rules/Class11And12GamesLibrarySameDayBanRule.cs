using Google.OrTools.Sat;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat.Rules;

public sealed class Class11And12GamesLibrarySameDayBanRule : IConstraintRule
{
    public string RuleId => "G2";

    public IReadOnlyList<string> Apply(CpModel model, SchoolModel school, TimetableVariables variables)
    {
        var targetGrades = new[] { "11", "12" };

        foreach (var section in school.SchedulableSections.Where(s => targetGrades.Contains(s.Grade)))
        {
            var gamesItem = section.Curriculum.FirstOrDefault(c => c.IsSchedulable && IsExactly(c.Name, "Games"));
            var libraryItem = section.Curriculum.FirstOrDefault(c => c.IsSchedulable && IsExactly(c.Name, "Library"));

            if (gamesItem is null || libraryItem is null)
            {
                continue;
            }

            foreach (var day in school.BellSchedule.WorkingDays)
            {
                var gamesVars = variables.ForSectionItemDay(section.Id, gamesItem.Id, day);
                var libraryVars = variables.ForSectionItemDay(section.Id, libraryItem.Id, day);

                if (gamesVars.Count == 0 || libraryVars.Count == 0)
                {
                    continue;
                }

                var gamesUsedToday = model.NewBoolVar($"gamesUsed[{section.Id}|{day}]");
                var libraryUsedToday = model.NewBoolVar($"libraryUsed[{section.Id}|{day}]");

                model.AddMaxEquality(gamesUsedToday, gamesVars);
                model.AddMaxEquality(libraryUsedToday, libraryVars);

                model.Add(gamesUsedToday + libraryUsedToday <= 1);
            }
        }

        return Array.Empty<string>();
    }

    private static bool IsExactly(string name, string target) =>
        string.Equals(name.Trim(), target, StringComparison.OrdinalIgnoreCase);
}
