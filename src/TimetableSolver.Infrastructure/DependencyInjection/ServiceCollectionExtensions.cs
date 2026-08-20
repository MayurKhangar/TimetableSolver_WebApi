using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Application.Options;
using TimetableSolver.Infrastructure.Repositories;
using TimetableSolver.Infrastructure.Services;

namespace TimetableSolver.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimetableInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.Configure<DataSourceOptions>(configuration.GetSection(DataSourceOptions.SectionName));

        services.AddSingleton<ISectionRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DataSourceOptions>>().Value;
            var path = ResolvePath(contentRootPath, options.BasePath, options.SectionsFile);
            return new JsonSectionRepository(path, sp.GetRequiredService<ILogger<JsonSectionRepository>>());
        });

        services.AddSingleton<IBellScheduleRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DataSourceOptions>>().Value;
            var path = ResolvePath(contentRootPath, options.BasePath, options.BellScheduleFile);
            return new JsonBellScheduleRepository(path, sp.GetRequiredService<ILogger<JsonBellScheduleRepository>>());
        });

        services.AddSingleton<ICurriculumRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DataSourceOptions>>().Value;
            var path = ResolvePath(contentRootPath, options.BasePath, options.ClassWiseSubjectsFile);
            return new MarkdownCurriculumRepository(path, sp.GetRequiredService<ILogger<MarkdownCurriculumRepository>>());
        });

        services.AddSingleton<ITeacherAssignmentRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DataSourceOptions>>().Value;
            var path = ResolvePath(contentRootPath, options.BasePath, options.TeacherClassAssignmentsFile);
            return new MarkdownTeacherAssignmentRepository(path, sp.GetRequiredService<ILogger<MarkdownTeacherAssignmentRepository>>());
        });

        services.AddSingleton<ISampleSchoolRepository>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DataSourceOptions>>().Value;
            var path = ResolvePath(contentRootPath, options.BasePath, options.SampleSchoolFile);
            return new JsonSampleSchoolRepository(path, sp.GetRequiredService<ILogger<JsonSampleSchoolRepository>>());
        });

        var mode = configuration.GetSection(DataSourceOptions.SectionName)["Mode"] ?? "FullDataset";
        if (string.Equals(mode, "Sample", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISchoolDataService, SampleSchoolDataService>();
        }
        else
        {
            services.AddSingleton<ISchoolDataService, FullDatasetSchoolDataService>();
        }

        services.AddSingleton<ITimetableStateStore, InMemoryTimetableStateStore>();

        return services;
    }

    private static string ResolvePath(string contentRootPath, string basePath, string fileName) =>
        Path.IsPathRooted(basePath)
            ? Path.Combine(basePath, fileName)
            : Path.Combine(contentRootPath, basePath, fileName);
}
