using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Domain.Entities;

public sealed record ScheduledLesson(
    string SectionId,
    string SectionDisplayName,
    SchoolDay Day,
    int Period,
    string ItemName,
    string TeacherId,
    string TeacherCode,
    string TeacherName);
