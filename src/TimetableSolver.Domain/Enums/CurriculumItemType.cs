namespace TimetableSolver.Domain.Enums;

/// <summary>
/// Distinguishes a formal academic subject from a whole-school / rotating activity group.
/// Mirrors the "Subject" vs "Activity Group" split used in CLASS_WISE_SUBJECTS.md.
/// </summary>
public enum CurriculumItemType
{
    Subject = 0,
    Activity = 1
}
