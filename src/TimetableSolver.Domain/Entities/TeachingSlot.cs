using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Domain.Entities;

public sealed record TeachingSlot(SchoolDay Day, int Period, string StartTime, string EndTime, int PairGroup)
{
    public string Key => $"{Day}-P{Period}";
}
