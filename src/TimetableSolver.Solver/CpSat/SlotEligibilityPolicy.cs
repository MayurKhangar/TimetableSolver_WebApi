using TimetableSolver.Domain.Entities;

namespace TimetableSolver.Solver.CpSat;

public interface ISlotEligibilityPolicy
{
    bool IsEligible(Section section, CurriculumItem item, TeachingSlot slot);
}

public sealed class DefaultSlotEligibilityPolicy : ISlotEligibilityPolicy
{
    public string RuleId => "G1";

    public bool IsEligible(Section section, CurriculumItem item, TeachingSlot slot)
    {
        if (slot.PairGroup == 0 && IsGamesOrLibrary(item.Name))
        {
            return false;
        }

        return true;
    }

    private static bool IsGamesOrLibrary(string name) =>
        name.Contains("Games", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Library", StringComparison.OrdinalIgnoreCase);
}
