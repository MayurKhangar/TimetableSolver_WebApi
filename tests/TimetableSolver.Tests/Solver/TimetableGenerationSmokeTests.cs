using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TimetableSolver.Application.Options;
using TimetableSolver.Domain.Enums;
using TimetableSolver.Infrastructure.Repositories;
using TimetableSolver.Infrastructure.Services;
using TimetableSolver.Solver;
using TimetableSolver.Solver.CpSat;
using TimetableSolver.Solver.CpSat.Rules;
using Xunit;

namespace TimetableSolver.Tests.Solver;

public sealed class TimetableGenerationSmokeTests
{
    private static string TestDataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    [Fact]
    public async Task GenerateAsync_OnSampleSchool_ProducesAFeasibleConflictFreeTimetable()
    {
        var sampleRepository = new JsonSampleSchoolRepository(TestDataPath("school-sample.json"), NullLogger<JsonSampleSchoolRepository>.Instance);
        var schoolDataService = new SampleSchoolDataService(sampleRepository);
        var school = await schoolDataService.LoadSchoolModelAsync();

        var solverOptions = new SolverOptions { TimeLimitSeconds = 20, Workers = 4, EnableSoftObjective = true };

        var rules = new List<IConstraintRule>
        {
            new NoTeacherDoubleBookingRule(),
            new OneLessonPerSectionSlotRule(),
            new DailySubjectMaxRule(),
            new WeeklyCurriculumTotalsRule(),
            new SameFirstWordSameDayBanRule(),
            new Class11And12GamesLibrarySameDayBanRule(),
            new BlockConsecutivePairingRule(),
            new TeacherWorkloadCapRule()
        };

        var modelBuilder = new CpSatModelBuilder(
            new DefaultSlotEligibilityPolicy(),
            rules,
            new MathMorningPreferenceObjective(),
            solverOptions,
            NullLogger<CpSatModelBuilder>.Instance);

        var solverEngine = new CpSatSolverEngine(solverOptions, NullLogger<CpSatSolverEngine>.Instance);
        var generationService = new OrToolsTimetableGenerationService(
            modelBuilder, solverEngine, solverOptions, NullLogger<OrToolsTimetableGenerationService>.Instance);

        var result = await generationService.GenerateAsync(school);

        result.Status.Should().BeOneOf(SolverStatus.Optimal, SolverStatus.Feasible);
        result.Success.Should().BeTrue();
        result.Lessons.Should().NotBeEmpty();

        result.Lessons
            .GroupBy(l => (l.TeacherId, l.Day, l.Period))
            .Should().OnlyContain(g => g.Count() == 1, "no teacher should be double-booked");

        result.Lessons
            .GroupBy(l => (l.SectionId, l.Day, l.Period))
            .Should().OnlyContain(g => g.Count() == 1, "no section should have two lessons in the same slot");

        foreach (var section in school.SchedulableSections)
        {
            foreach (var item in section.Curriculum.Where(c => c.IsSchedulable))
            {
                var scheduledCount = result.Lessons.Count(l => l.SectionId == section.Id && l.ItemName == item.Name);
                scheduledCount.Should().Be(item.PeriodsPerWeek, $"{section.DisplayName}/{item.Name} must receive exactly its weekly quota");
            }
        }
    }
}
