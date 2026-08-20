using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Application.Options;
using TimetableSolver.Solver.CpSat;
using TimetableSolver.Solver.CpSat.Rules;

namespace TimetableSolver.Solver.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimetableSolver(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SolverOptions>(configuration.GetSection(SolverOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SolverOptions>>().Value);

        services.AddSingleton<ISlotEligibilityPolicy, DefaultSlotEligibilityPolicy>();
        services.AddSingleton<MathMorningPreferenceObjective>();

        services.AddSingleton<IReadOnlyList<IConstraintRule>>(_ => new List<IConstraintRule>
        {
            new NoTeacherDoubleBookingRule(),
            new OneLessonPerSectionSlotRule(),
            new DailySubjectMaxRule(),
            new WeeklyCurriculumTotalsRule(),
            new SameFirstWordSameDayBanRule(),
            new Class11And12GamesLibrarySameDayBanRule(),

            new BlockConsecutivePairingRule(),

            new TeacherWorkloadCapRule(),
        });

        services.AddSingleton<CpSatModelBuilder>();
        services.AddSingleton<CpSatSolverEngine>();
        services.AddSingleton<ITimetableGenerationService, OrToolsTimetableGenerationService>();

        return services;
    }
}
