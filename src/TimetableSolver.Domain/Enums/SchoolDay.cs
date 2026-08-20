namespace TimetableSolver.Domain.Enums;

/// <summary>
/// The six working days supported by the bell schedule. Numeric values intentionally
/// mirror the 1 = Monday .. 6 = Saturday convention documented in the source data.
/// </summary>
public enum SchoolDay
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6
}
