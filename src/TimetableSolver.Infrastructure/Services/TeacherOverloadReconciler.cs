using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Infrastructure.Services;

/// <summary>
/// Resolves the one structural consequence of only having class-level teacher assignments
/// (see README §5): the same teacher gets assigned to every section of a grade at once, which
/// can push a teacher's aggregate weekly load above the bell schedule's capacity. Left alone,
/// that makes the *entire* CP-SAT model infeasible — one overloaded teacher blocks every other
/// section too, even ones with no data problems at all.
///
/// This runs once, after a <see cref="SchoolModel"/>'s sections and curriculum are fully built,
/// and for every teacher over capacity, excludes just enough of their assigned curriculum items
/// (in a stable, deterministic order) to bring them back within capacity. Each exclusion is
/// reported as a <see cref="DataConflictType.TeacherOverload"/> conflict — matching the brief's
/// "exclude only rows with documented data conflicts" allowance (AC-3) rather than silently
/// dropping anything or failing the whole run.
/// </summary>
public static class TeacherOverloadReconciler
{
    public static void Reconcile(IReadOnlyList<Section> sections, IReadOnlyDictionary<string, Teacher> teachersById, int weeklyCapacity, List<DataConflict> conflicts)
    {
        var assignedItems = sections
            .SelectMany(section => section.Curriculum
                .Where(item => item.IsSchedulable)
                .Select(item => (Section: section, Item: item)))
            .GroupBy(x => x.Item.TeacherId!);

        foreach (var teacherGroup in assignedItems)
        {
            var totalLoad = teacherGroup.Sum(x => x.Item.PeriodsPerWeek);
            if (totalLoad <= weeklyCapacity || !teachersById.TryGetValue(teacherGroup.Key, out var teacher))
            {
                continue;
            }

            // Deterministic order (by section id, then item name) so re-runs always exclude the same rows.
            var ordered = teacherGroup
                .OrderBy(x => x.Section.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var remaining = totalLoad;

            foreach (var (section, item) in ordered)
            {
                if (remaining <= weeklyCapacity)
                {
                    break;
                }

                item.TeacherId = null;
                remaining -= item.PeriodsPerWeek;

                conflicts.Add(new DataConflict
                {
                    Type = DataConflictType.TeacherOverload,
                    SectionId = section.Id,
                    SectionDisplayName = section.DisplayName,
                    ItemName = item.Name,
                    Message = $"{section.DisplayName}/{item.Name}: teacher {teacher.Code} ({teacher.Name}) is already " +
                              $"assigned {totalLoad} periods/week across every section of this grade (class-level " +
                              $"assignment data applies the same teacher to all of them - see README §5), but only " +
                              $"{weeklyCapacity} teaching slots exist per week. Excluded from scheduling until " +
                              "section-level assignments are available."
                });
            }
        }
    }
}
