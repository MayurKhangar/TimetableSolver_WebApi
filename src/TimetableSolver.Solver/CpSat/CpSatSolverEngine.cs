using System.Diagnostics;
using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Options;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Solver.CpSat;

public sealed record CpSatSolveOutcome(CpSolverStatus RawStatus, SolverStatus Status, long WallTimeMs, double? ObjectiveValue, CpSolver Solver);

public sealed class CpSatSolverEngine
{
    private readonly SolverOptions _options;
    private readonly ILogger<CpSatSolverEngine> _logger;

    public CpSatSolverEngine(SolverOptions options, ILogger<CpSatSolverEngine> logger)
    {
        _options = options;
        _logger = logger;
    }

    public CpSatSolveOutcome Solve(CpModel model)
    {
        // Validate the model before giving it to CP-SAT
        var validation = model.Validate();

        if (!string.IsNullOrWhiteSpace(validation))
        {
            _logger.LogError(
                "CP-SAT model validation failed: {Validation}",
                validation);
        }
        else
        {
            _logger.LogInformation("CP-SAT model validation passed.");
        }
        var solver = new CpSolver
        {
            StringParameters = $"max_time_in_seconds:{_options.TimeLimitSeconds};num_search_workers:{_options.Workers}"
        };

        _logger.LogInformation("Starting CP-SAT solve. TimeLimit={TimeLimit}s, Workers={Workers}",
       _options.TimeLimitSeconds, _options.Workers);

        var stopwatch = Stopwatch.StartNew();
        var rawStatus = solver.Solve(model);
        stopwatch.Stop();

        _logger.LogInformation("CP-SAT solve finished with status {Status} in {ElapsedMs}ms", rawStatus, stopwatch.ElapsedMilliseconds);

        var status = MapStatus(rawStatus);


        double? objective = status is SolverStatus.Optimal or SolverStatus.Feasible ? solver.ObjectiveValue : null;

        return new CpSatSolveOutcome(rawStatus, status, stopwatch.ElapsedMilliseconds, objective, solver);
    }

    private static SolverStatus MapStatus(CpSolverStatus status) => status switch
    {
        CpSolverStatus.Optimal => SolverStatus.Optimal,
        CpSolverStatus.Feasible => SolverStatus.Feasible,
        CpSolverStatus.Infeasible => SolverStatus.Infeasible,
        CpSolverStatus.ModelInvalid => SolverStatus.ModelInvalid,
        CpSolverStatus.Unknown => SolverStatus.Unknown,
        _ => SolverStatus.Unknown
    };
}
