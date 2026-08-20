using Google.OrTools.Sat;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Options;
using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat;

public sealed class CpSatModelBuilder
{
    private readonly ISlotEligibilityPolicy _eligibilityPolicy;
    private readonly IReadOnlyList<IConstraintRule> _rules;
    private readonly MathMorningPreferenceObjective _softObjective;
    private readonly SolverOptions _options;
    private readonly ILogger<CpSatModelBuilder> _logger;

    public CpSatModelBuilder(
        ISlotEligibilityPolicy eligibilityPolicy,
        IReadOnlyList<IConstraintRule> rules,
        MathMorningPreferenceObjective softObjective,
        SolverOptions options,
        ILogger<CpSatModelBuilder> logger)
    {
        _eligibilityPolicy = eligibilityPolicy;
        _rules = rules;
        _softObjective = softObjective;
        _options = options;
        _logger = logger;
    }

    public (CpModel Model, TimetableVariables Variables, IReadOnlyList<string> RuleNotes) Build(SchoolModel school)
    {
        var model = new CpModel();
        var variables = new TimetableVariables();

        foreach (var section in school.SchedulableSections)
        {
            foreach (var item in section.Curriculum.Where(c => c.IsSchedulable))
            {
                foreach (var day in school.BellSchedule.WorkingDays)
                {
                    foreach (var slot in school.BellSchedule.SlotsOn(day))
                    {
                        if (_eligibilityPolicy.IsEligible(section, item, slot))
                        {
                            variables.Create(model, section, item, slot);
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Created {Count} lesson decision variables", variables.All.Count);

        var ruleNotes = new List<string>();

        foreach (var rule in _rules)
        {
            var notes = rule.Apply(model, school, variables);
            _logger.LogDebug("Applied constraint rule {RuleId}", rule.RuleId);

            foreach (var note in notes)
            {
                _logger.LogWarning("{RuleId}: {Note}", rule.RuleId, note);
                ruleNotes.Add(note);
            }
        }

        if (_options.EnableSoftObjective)
        {
            var penalty = _softObjective.BuildPenaltyExpression(school, variables, penaltyWeight: 10);
            model.Minimize(penalty);
        }

        return (model, variables, ruleNotes);
    }
}
