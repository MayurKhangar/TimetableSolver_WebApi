using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Infrastructure.Services;

public sealed class SampleSchoolDataService : ISchoolDataService
{
    private readonly ISampleSchoolRepository _sampleSchoolRepository;

    public SampleSchoolDataService(ISampleSchoolRepository sampleSchoolRepository)
    {
        _sampleSchoolRepository = sampleSchoolRepository;
    }

    public Task<SchoolModel> LoadSchoolModelAsync(CancellationToken cancellationToken = default) =>
        _sampleSchoolRepository.GetSampleSchoolAsync(cancellationToken);
}
