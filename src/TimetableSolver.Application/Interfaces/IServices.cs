using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Application.Interfaces;

public interface ISchoolDataService
{
    Task<SchoolModel> LoadSchoolModelAsync(CancellationToken cancellationToken = default);
}

public interface ITimetableGenerationService
{
    Task<GenerationResult> GenerateAsync(SchoolModel school, CancellationToken cancellationToken = default);
}

public interface ITimetableStateStore
{
    SchoolModel? CurrentSchool { get; }
    GenerationResult? LastGenerationResult { get; }

    void SetSchool(SchoolModel school);
    void SetGenerationResult(GenerationResult result);
}
