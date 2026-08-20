using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Domain.Entities;

public sealed class BellSchedule
{
    public required IReadOnlyList<SchoolDay> WorkingDays { get; init; }
    public required IReadOnlyList<TeachingSlot> Slots { get; init; }

    public int WeeklyTeachingCapacity => Slots.Count;

    public IEnumerable<TeachingSlot> SlotsOn(SchoolDay day) => Slots.Where(s => s.Day == day);

    public IEnumerable<int> PairGroupsOn(SchoolDay day) =>
        SlotsOn(day).Select(s => s.PairGroup).Distinct().OrderBy(g => g);

    public IEnumerable<TeachingSlot> SlotsInPairGroup(SchoolDay day, int pairGroup) =>
        SlotsOn(day).Where(s => s.PairGroup == pairGroup).OrderBy(s => s.Period);
}
