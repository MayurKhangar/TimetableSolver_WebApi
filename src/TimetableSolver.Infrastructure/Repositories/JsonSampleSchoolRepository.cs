using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Exceptions;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;
using TimetableSolver.Infrastructure.Parsing;

namespace TimetableSolver.Infrastructure.Repositories;

public sealed class JsonSampleSchoolRepository : ISampleSchoolRepository
{
    private static readonly string[] ActivityKeywords =
    {
        "Games", "Library", "Karate", "Happy Feet", "Gymnastics", "Skating", "Band", "Robotics", "Abacus"
    };

    private readonly string _filePath;
    private readonly ILogger<JsonSampleSchoolRepository> _logger;

    public JsonSampleSchoolRepository(string filePath, ILogger<JsonSampleSchoolRepository> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<SchoolModel> GetSampleSchoolAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new DataLoadException($"Sample school file not found: {_filePath}", _filePath);
        }

        SampleFileModel model;
        try
        {
            await using var stream = File.OpenRead(_filePath);
            model = await JsonSerializer.DeserializeAsync<SampleFileModel>(stream, JsonDefaults.Options, cancellationToken)
                ?? throw new DataLoadException("school-sample.json deserialized to null.", _filePath);
        }
        catch (JsonException ex)
        {
            throw new DataLoadException($"school-sample.json is not valid JSON: {ex.Message}", _filePath, ex);
        }

        var teachersById = model.Teachers.ToDictionary(
            t => t.Id,
            t => new Teacher
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                MaxPeriodsPerDay = t.MaxPeriodsPerDay,
                MaxPeriodsPerWeek = t.MaxPeriodsPerWeek
            });

        var conflicts = new List<DataConflict>();
        var sections = new List<Section>();

        foreach (var rawSection in model.ClassSections)
        {
            var section = new Section
            {
                Id = rawSection.Id,
                DisplayName = rawSection.DisplayName,
                Grade = rawSection.Grade,
                SectionName = rawSection.Section,
                CurriculumKey = rawSection.DisplayName,
                Capacity = 0
            };

            foreach (var item in rawSection.Curriculum)
            {
                var teacherExists = item.TeacherId is not null && teachersById.ContainsKey(item.TeacherId);
                if (item.TeacherId is not null && !teacherExists)
                {
                    conflicts.Add(new DataConflict
                    {
                        Type = DataConflictType.MissingAssignment,
                        SectionId = section.Id,
                        SectionDisplayName = section.DisplayName,
                        ItemName = item.Name,
                        Message = $"Teacher id '{item.TeacherId}' referenced by {section.DisplayName}/{item.Name} is not in the teacher roster."
                    });
                }

                if (item.PeriodsPerWeek <= 0)
                {
                    conflicts.Add(new DataConflict
                    {
                        Type = DataConflictType.ZeroWorkload,
                        SectionId = section.Id,
                        SectionDisplayName = section.DisplayName,
                        ItemName = item.Name,
                        Message = $"{section.DisplayName}/{item.Name} has periodsPerWeek <= 0 and is excluded from scheduling."
                    });
                }

                section.Curriculum.Add(new CurriculumItem
                {
                    Id = $"{section.Id}::{item.Name}",
                    SectionId = section.Id,
                    Name = item.Name,
                    Type = ClassifyType(item.Name),
                    PeriodsPerWeek = item.PeriodsPerWeek,
                    MaxPeriodsPerDay = item.MaxPeriodsPerDay,
                    TeacherId = teacherExists ? item.TeacherId : null,
                    IsUnassignedPlaceholder = false
                });
            }

            sections.Add(section);
        }

        var bellSchedule = BuildBellSchedule(model.BellSchedule);

        _logger.LogInformation("Loaded sample school with {Sections} sections and {Teachers} teachers from {File}",
            sections.Count, teachersById.Count, Path.GetFileName(_filePath));

        return new SchoolModel
        {
            AcademicYear = model.AcademicYear,
            BellSchedule = bellSchedule,
            Sections = sections,
            TeachersById = teachersById,
            DataConflicts = conflicts
        };
    }

    private static CurriculumItemType ClassifyType(string name) =>
        ActivityKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase))
            ? CurriculumItemType.Activity
            : CurriculumItemType.Subject;

    private static BellSchedule BuildBellSchedule(BellScheduleModel raw)
    {
        var workingDays = new[]
        {
            SchoolDay.Monday, SchoolDay.Tuesday, SchoolDay.Wednesday, SchoolDay.Thursday, SchoolDay.Friday, SchoolDay.Saturday
        };

        var slots = new List<TeachingSlot>();
        foreach (var day in workingDays)
        {
            var isFridayOrSaturday = day is SchoolDay.Friday or SchoolDay.Saturday;
            var periods = isFridayOrSaturday ? raw.FridayAndSaturday.TeachingPeriods : raw.MondayToThursday.TeachingPeriods;

            foreach (var period in periods.OrderBy(p => p))
            {
                slots.Add(new TeachingSlot(day, period, string.Empty, string.Empty, (period - 1) / 2));
            }
        }

        return new BellSchedule { WorkingDays = workingDays, Slots = slots };
    }

    private sealed record SampleFileModel(
        [property: JsonPropertyName("academicYear")] string AcademicYear,
        [property: JsonPropertyName("bellSchedule")] BellScheduleModel BellSchedule,
        [property: JsonPropertyName("classSections")] List<SampleSectionModel> ClassSections,
        [property: JsonPropertyName("teachers")] List<SampleTeacherModel> Teachers);

    private sealed record BellScheduleModel(
        [property: JsonPropertyName("mondayToThursday")] BellScheduleDayModel MondayToThursday,
        [property: JsonPropertyName("fridayAndSaturday")] BellScheduleDayModel FridayAndSaturday);

    private sealed record BellScheduleDayModel(
        [property: JsonPropertyName("teachingPeriods")] List<int> TeachingPeriods);

    private sealed record SampleSectionModel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("grade")] string Grade,
        [property: JsonPropertyName("section")] string Section,
        [property: JsonPropertyName("curriculum")] List<SampleCurriculumModel> Curriculum);

    private sealed record SampleCurriculumModel(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("periodsPerWeek")] int PeriodsPerWeek,
        [property: JsonPropertyName("maxPeriodsPerDay")] int MaxPeriodsPerDay,
        [property: JsonPropertyName("teacherId")] string? TeacherId);

    private sealed record SampleTeacherModel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("maxPeriodsPerDay")] int? MaxPeriodsPerDay,
        [property: JsonPropertyName("maxPeriodsPerWeek")] int? MaxPeriodsPerWeek);
}
