using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;

namespace TimetableSolver.Infrastructure.Services;

public sealed class FullDatasetSchoolDataService : ISchoolDataService
{
    private readonly ISectionRepository _sectionRepository;
    private readonly ICurriculumRepository _curriculumRepository;
    private readonly ITeacherAssignmentRepository _teacherAssignmentRepository;
    private readonly IBellScheduleRepository _bellScheduleRepository;
    private readonly ILogger<FullDatasetSchoolDataService> _logger;

    public FullDatasetSchoolDataService(
        ISectionRepository sectionRepository,
        ICurriculumRepository curriculumRepository,
        ITeacherAssignmentRepository teacherAssignmentRepository,
        IBellScheduleRepository bellScheduleRepository,
        ILogger<FullDatasetSchoolDataService> logger)
    {
        _sectionRepository = sectionRepository;
        _curriculumRepository = curriculumRepository;
        _teacherAssignmentRepository = teacherAssignmentRepository;
        _bellScheduleRepository = bellScheduleRepository;
        _logger = logger;
    }

    public async Task<SchoolModel> LoadSchoolModelAsync(CancellationToken cancellationToken = default)
    {
        var rawSectionsTask = _sectionRepository.GetSectionsAsync(cancellationToken);
        var curriculumRowsTask = _curriculumRepository.GetCurriculumAsync(cancellationToken);
        var teacherRosterTask = _teacherAssignmentRepository.GetTeacherRosterAsync(cancellationToken);
        var assignmentRowsTask = _teacherAssignmentRepository.GetAssignmentRowsAsync(cancellationToken);
        var bellScheduleTask = _bellScheduleRepository.GetBellScheduleAsync(cancellationToken);

        await Task.WhenAll(rawSectionsTask, curriculumRowsTask, teacherRosterTask, assignmentRowsTask, bellScheduleTask);

        var rawSections = await rawSectionsTask;
        var curriculumRows = await curriculumRowsTask;
        var teacherRoster = await teacherRosterTask;
        var assignmentRows = await assignmentRowsTask;
        var bellSchedule = await bellScheduleTask;

        var teachersByCode = teacherRoster.ToDictionary(t => t.Code, t => t, StringComparer.OrdinalIgnoreCase);
        var curriculumByKey = curriculumRows
            .GroupBy(r => r.CurriculumKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // (ClassLabel, ItemName) -> assignment rows for that class-subject, real teachers only.
        var realAssignmentsByKey = assignmentRows
            .Where(r => !r.IsUnassignedPlaceholder)
            .GroupBy(r => (r.ClassLabel, r.ItemName), AssignmentKeyComparer.Instance)
            .ToDictionary(g => g.Key, g => g.ToList(), AssignmentKeyComparer.Instance);

        var placeholderKeys = assignmentRows
            .Where(r => r.IsUnassignedPlaceholder)
            .Select(r => (r.ClassLabel, r.ItemName))
            .ToHashSet(AssignmentKeyComparer.Instance);

        var conflicts = new List<DataConflict>();
        var sections = new List<Section>();

        foreach (var raw in rawSections)
        {
            var section = new Section
            {
                Id = raw.Id,
                DisplayName = raw.DisplayName,
                Grade = raw.Grade,
                SectionName = raw.SectionName,
                CurriculumKey = raw.CurriculumKey,
                Capacity = raw.Capacity
            };

            if (!curriculumByKey.TryGetValue(raw.CurriculumKey, out var curriculumForClass))
            {
                conflicts.Add(new DataConflict
                {
                    Type = DataConflictType.MissingCurriculum,
                    SectionId = section.Id,
                    SectionDisplayName = section.DisplayName,
                    Message = $"No curriculum defined for '{raw.CurriculumKey}' in CLASS_WISE_SUBJECTS.md " +
                              "(expected for Pre-Primary/KG - see sections.json note). Section excluded from generation."
                });
                sections.Add(section);
                continue;
            }

            foreach (var curriculumRow in curriculumForClass)
            {
                var item = ResolveCurriculumItem(section, curriculumRow, realAssignmentsByKey, placeholderKeys, teachersByCode, conflicts);
                section.Curriculum.Add(item);
            }

            sections.Add(section);
        }

        _logger.LogInformation(
            "Built full-dataset school model: {Sections} sections, {Teachers} teachers, {Conflicts} data conflicts",
            sections.Count, teachersByCode.Count, conflicts.Count);

        return new SchoolModel
        {
            AcademicYear = "2026-27",
            BellSchedule = bellSchedule,
            Sections = sections,
            TeachersById = teachersByCode.Values.ToDictionary(t => t.Id, t => t),
            DataConflicts = conflicts
        };
    }

    private static CurriculumItem ResolveCurriculumItem(
        Section section,
        Application.Interfaces.RawCurriculumRow curriculumRow,
        Dictionary<(string ClassLabel, string ItemName), List<Application.Interfaces.RawAssignmentRow>> realAssignmentsByKey,
        HashSet<(string ClassLabel, string ItemName)> placeholderKeys,
        Dictionary<string, Teacher> teachersByCode,
        List<DataConflict> conflicts)
    {
        var itemId = $"{section.Id}::{curriculumRow.Name}";
        var key = (section.CurriculumKey, curriculumRow.Name);

        if (realAssignmentsByKey.TryGetValue(key, out var matches) && matches.Count > 0)
        {
            var chosen = matches.Count == 1 ? matches[0] : PickBestMatch(matches, section, curriculumRow.Name, conflicts);

            if (chosen.PeriodsPerWeekForClass <= 0)
            {
                conflicts.Add(new DataConflict
                {
                    Type = DataConflictType.ZeroWorkload,
                    SectionId = section.Id,
                    SectionDisplayName = section.DisplayName,
                    ItemName = curriculumRow.Name,
                    Message = $"{section.DisplayName}/{curriculumRow.Name}: teacher {chosen.TeacherCode} ({chosen.TeacherName}) " +
                              "has a class-level workload of 0 in TEACHER_CLASS_ASSIGNMENTS.md. Excluded from scheduling (rule H10)."
                });

                return BuildItem(itemId, section, curriculumRow, teacherId: null, isPlaceholder: false);
            }

            if (!teachersByCode.TryGetValue(chosen.TeacherCode, out var teacher))
            {
                conflicts.Add(new DataConflict
                {
                    Type = DataConflictType.MissingAssignment,
                    SectionId = section.Id,
                    SectionDisplayName = section.DisplayName,
                    ItemName = curriculumRow.Name,
                    Message = $"{section.DisplayName}/{curriculumRow.Name}: teacher code '{chosen.TeacherCode}' is not present " +
                              "in the teacher roster (section 2 of TEACHER_CLASS_ASSIGNMENTS.md)."
                });
                return BuildItem(itemId, section, curriculumRow, teacherId: null, isPlaceholder: false);
            }

            return BuildItem(itemId, section, curriculumRow, teacherId: teacher.Id, isPlaceholder: false);
        }

        if (placeholderKeys.Contains(key))
        {
            conflicts.Add(new DataConflict
            {
                Type = DataConflictType.UnassignedTeacher,
                SectionId = section.Id,
                SectionDisplayName = section.DisplayName,
                ItemName = curriculumRow.Name,
                Message = $"{section.DisplayName}/{curriculumRow.Name}: assignment row uses UNASSIGNED-TT - " +
                          "cannot schedule until a teacher is assigned."
            });
            return BuildItem(itemId, section, curriculumRow, teacherId: null, isPlaceholder: true);
        }

        conflicts.Add(new DataConflict
        {
            Type = DataConflictType.MissingAssignment,
            SectionId = section.Id,
            SectionDisplayName = section.DisplayName,
            ItemName = curriculumRow.Name,
            Message = $"{section.DisplayName}/{curriculumRow.Name}: no teaching assignment found for " +
                      $"'{section.CurriculumKey}' / '{curriculumRow.Name}' in TEACHER_CLASS_ASSIGNMENTS.md."
        });
        return BuildItem(itemId, section, curriculumRow, teacherId: null, isPlaceholder: false);
    }

 
    private static Application.Interfaces.RawAssignmentRow PickBestMatch(
        List<Application.Interfaces.RawAssignmentRow> matches, Section section, string itemName, List<DataConflict> conflicts)
    {
        var ordered = matches.OrderByDescending(m => m.PeriodsPerWeekForClass).ToList();
        var chosen = ordered[0];

        conflicts.Add(new DataConflict
        {
            Type = DataConflictType.AmbiguousAssignment,
            SectionId = section.Id,
            SectionDisplayName = section.DisplayName,
            ItemName = itemName,
            Message = $"{section.DisplayName}/{itemName}: {matches.Count} teachers listed for the same class-subject " +
                      $"({string.Join(", ", matches.Select(m => m.TeacherCode))}). Using '{chosen.TeacherCode}' " +
                      "(highest reported workload); the others were not applied to this section."
        });

        return chosen;
    }

    private static CurriculumItem BuildItem(
        string id, Section section, Application.Interfaces.RawCurriculumRow curriculumRow, string? teacherId, bool isPlaceholder) => new()
    {
        Id = id,
        SectionId = section.Id,
        Name = curriculumRow.Name,
        Type = curriculumRow.Type,
        PeriodsPerWeek = curriculumRow.PeriodsPerWeek,
        MaxPeriodsPerDay = curriculumRow.PeriodsPerDay,
        TeacherId = teacherId,
        IsUnassignedPlaceholder = isPlaceholder
    };

    private sealed class AssignmentKeyComparer : IEqualityComparer<(string ClassLabel, string ItemName)>
    {
        public static readonly AssignmentKeyComparer Instance = new();

        public bool Equals((string ClassLabel, string ItemName) x, (string ClassLabel, string ItemName) y) =>
            string.Equals(x.ClassLabel, y.ClassLabel, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ItemName, y.ItemName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ClassLabel, string ItemName) obj) =>
            HashCode.Combine(obj.ClassLabel.ToUpperInvariant(), obj.ItemName.ToUpperInvariant());
    }
}
