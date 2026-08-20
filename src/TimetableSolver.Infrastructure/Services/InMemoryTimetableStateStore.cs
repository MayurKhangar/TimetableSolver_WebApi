using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Infrastructure.Services;

/// <summary>
/// Thread-safe, process-lifetime cache of the most recently loaded school model and the most
/// recent solve. Registered as a singleton. See <see cref="ITimetableStateStore"/> for why a
/// database-backed store is the natural production replacement.
/// </summary>
public sealed class InMemoryTimetableStateStore : ITimetableStateStore
{
    private readonly object _lock = new();
    private SchoolModel? _school;
    private GenerationResult? _lastResult;

    public SchoolModel? CurrentSchool
    {
        get { lock (_lock) { return _school; } }
    }

    public GenerationResult? LastGenerationResult
    {
        get { lock (_lock) { return _lastResult; } }
    }

    public void SetSchool(SchoolModel school)
    {
        lock (_lock) { _school = school; }
    }

    public void SetGenerationResult(GenerationResult result)
    {
        lock (_lock) { _lastResult = result; }
    }
}
