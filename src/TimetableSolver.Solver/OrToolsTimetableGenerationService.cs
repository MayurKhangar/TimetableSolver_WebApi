using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Exceptions;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Application.Options;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;
using TimetableSolver.Solver.CpSat;

namespace TimetableSolver.Solver;

public sealed class OrToolsTimetableGenerationService : ITimetableGenerationService
{
    private readonly CpSatModelBuilder _modelBuilder;
    private readonly CpSatSolverEngine _solverEngine;
    private readonly SolverOptions _options;
    private readonly ILogger<OrToolsTimetableGenerationService> _logger;

    public OrToolsTimetableGenerationService(
        CpSatModelBuilder modelBuilder,
        CpSatSolverEngine solverEngine,
        SolverOptions options,
        ILogger<OrToolsTimetableGenerationService> logger)
    {
        _modelBuilder = modelBuilder;
        _solverEngine = solverEngine;
        _options = options;
        _logger = logger;
    }

    public Task<GenerationResult> GenerateAsync(SchoolModel school, CancellationToken cancellationToken = default)
    {
        try
        {
            var (model, variables, ruleNotes) = _modelBuilder.Build(school);
            var outcome = _solverEngine.Solve(model);

            var isSolved = outcome.Status is SolverStatus.Optimal or SolverStatus.Feasible;
            var lessons = isSolved ? ExtractLessons(school, variables, outcome.Solver) : Array.Empty<ScheduledLesson>();

            var (dataConflicts, schedulingConflicts) = isSolved
                ? (school.DataConflicts, ruleNotes)
                : BuildFailureReport(school, ruleNotes);

            var scheduledSectionIds = lessons.Select(l => l.SectionId).Distinct().Count();

            var result = new GenerationResult
            {
                Status = outcome.Status,
                Success = isSolved,
                WallTimeMs = outcome.WallTimeMs,
                TimeLimitSeconds = _options.TimeLimitSeconds,
                ObjectiveValue = outcome.ObjectiveValue,
                Lessons = lessons,
                DataConflicts = dataConflicts,
                SchedulingConflicts = schedulingConflicts,
                SectionsTotal = school.Sections.Count,
                SectionsScheduled = scheduledSectionIds,
                TeachersInvolved = lessons.Select(l => l.TeacherId).Distinct().Count()
            };

            return Task.FromResult(result);
        }
        catch (Exception ex) when (ex is not TimetableGenerationException)
        {
            _logger.LogError(ex, "Unhandled error while generating the timetable");
            throw new TimetableGenerationException("Failed to build or solve the CP-SAT model.", ex);
        }
    }

    private static IReadOnlyList<ScheduledLesson> ExtractLessons(SchoolModel school, TimetableVariables variables, CpSolver solver)
    {
        var lessons = new List<ScheduledLesson>();
        var sectionsById = school.Sections.ToDictionary(s => s.Id, s => s);

        foreach (var ((sectionId, itemId, day, period), boolVar) in variables.All)
        {
            if (solver.Value(boolVar) != 1)
            {
                continue;
            }

            var section = sectionsById[sectionId];
            var item = section.Curriculum.First(c => c.Id == itemId);
            var teacher = school.TeachersById[item.TeacherId!];

            lessons.Add(new ScheduledLesson(
                SectionId: section.Id,
                SectionDisplayName: section.DisplayName,
                Day: day,
                Period: period,
                ItemName: item.Name,
                TeacherId: teacher.Id,
                TeacherCode: teacher.Code,
                TeacherName: teacher.Name));
        }

        return lessons.OrderBy(l => l.SectionDisplayName).ThenBy(l => l.Day).ThenBy(l => l.Period).ToList();
    }

    /// <summary>
    /// Builds the conflict report for a failed solve: any remaining teacher-overload cause (normally
    /// already pre-empted by <c>TeacherOverloadReconciler</c> for the FullDataset source, but checked
    /// again here as a safety net for any data source that doesn't run that reconciliation step) plus a
    /// generic fallback note when no single teacher is individually over capacity.
    /// </summary>
    private static (IReadOnlyList<DataConflict> DataConflicts, IReadOnlyList<string> SchedulingConflicts) BuildFailureReport(
        SchoolModel school, IReadOnlyList<string> ruleNotes)
    {
        var overloadConflicts = DiagnoseTeacherOverload(school);
        var dataConflicts = school.DataConflicts.Concat(overloadConflicts).ToList();

        var schedulingConflicts = overloadConflicts.Count > 0
            ? ruleNotes.Concat(overloadConflicts.Select(c => c.Message)).ToList()
            : ruleNotes.Append(
                "No single teacher exceeds weekly capacity, so infeasibility likely comes from a combination of " +
                "overlapping constraints (e.g. G1/G2/L1 interacting with a tight bell schedule) rather than one " +
                "obvious cause. Re-run with EnableSoftObjective=false or a longer TimeLimitSeconds, or inspect the " +
                "model per section.").ToList();

        return (dataConflicts, schedulingConflicts);
    }

    private static IReadOnlyList<DataConflict> DiagnoseTeacherOverload(SchoolModel school)
    {
        var conflicts = new List<DataConflict>();

        var loadByTeacher = school.Sections
            .SelectMany(s => s.Curriculum.Where(c => c.IsSchedulable))
            .GroupBy(c => c.TeacherId!)
            .Select(g => new { TeacherId = g.Key, TotalPeriods = g.Sum(c => c.PeriodsPerWeek) });

        foreach (var load in loadByTeacher)
        {
            if (load.TotalPeriods > school.BellSchedule.WeeklyTeachingCapacity && school.TeachersById.TryGetValue(load.TeacherId, out var teacher))
            {
                conflicts.Add(new DataConflict
                {
                    Type = DataConflictType.TeacherOverload,
                    Message = $"Teacher {teacher.Code} ({teacher.Name}): {load.TotalPeriods} periods required across all assigned " +
                              $"sections but only {school.BellSchedule.WeeklyTeachingCapacity} teaching slots exist per week."
                });
            }
        }

        return conflicts;
    }
}
