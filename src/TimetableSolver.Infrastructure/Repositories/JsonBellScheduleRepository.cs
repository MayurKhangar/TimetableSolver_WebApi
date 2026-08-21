using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Exceptions;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Domain.Enums;
using TimetableSolver.Infrastructure.Parsing;

namespace TimetableSolver.Infrastructure.Repositories;

public sealed class JsonBellScheduleRepository : IBellScheduleRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonBellScheduleRepository> _logger;

    public JsonBellScheduleRepository(string filePath, ILogger<JsonBellScheduleRepository> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<BellSchedule> GetBellScheduleAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new DataLoadException($"Bell schedule file not found: {_filePath}", _filePath);
        }

        BellScheduleFileModel model;
        try
        {
            await using var stream = File.OpenRead(_filePath);
            model = await JsonSerializer.DeserializeAsync<BellScheduleFileModel>(stream, JsonDefaults.Options, cancellationToken)
                ?? throw new DataLoadException("bell-schedule.json deserialized to null.", _filePath);
        }
        catch (JsonException ex)
        {
            throw new DataLoadException($"bell-schedule.json is not valid JSON: {ex.Message}", _filePath, ex);
        }

        var workingDays = model.WorkingDays
            .Select(ParseDay)
            .ToList();

        var slots = new List<TeachingSlot>();
        foreach (var day in workingDays)
        {
            var isFridayOrSaturday = day is SchoolDay.Friday or SchoolDay.Saturday;
            var periods = isFridayOrSaturday ? model.FridayAndSaturday.TeachingPeriods : model.MondayToThursday.TeachingPeriods;

            foreach (var period in periods.OrderBy(p => p.Period))
            {
                var pairGroup = (period.Period - 1) / 2;
                slots.Add(new TeachingSlot(day, period.Period, period.Start, period.End, pairGroup));
            }
        }

        _logger.LogInformation("Expanded bell schedule into {Count} teaching slots across {Days} working days from {File}",
            slots.Count, workingDays.Count, Path.GetFileName(_filePath));

        return new BellSchedule
        {
            WorkingDays = workingDays,
            Slots = slots
        };
    }

    private static SchoolDay ParseDay(string raw) => raw.Trim().ToUpperInvariant() switch
    {
        "MONDAY" => SchoolDay.Monday,
        "TUESDAY" => SchoolDay.Tuesday,
        "WEDNESDAY" => SchoolDay.Wednesday,
        "THURSDAY" => SchoolDay.Thursday,
        "FRIDAY" => SchoolDay.Friday,
        "SATURDAY" => SchoolDay.Saturday,
        _ => throw new DataLoadException($"Unrecognized working day '{raw}' in bell-schedule.json")
    };

    private sealed record BellScheduleFileModel(
        [property: JsonPropertyName("workingDays")] List<string> WorkingDays,
        [property: JsonPropertyName("mondayToThursday")] DayScheduleModel MondayToThursday,
        [property: JsonPropertyName("fridayAndSaturday")] DayScheduleModel FridayAndSaturday);

    private sealed record DayScheduleModel(
        [property: JsonPropertyName("teachingPeriods")] List<PeriodModel> TeachingPeriods);

    private sealed record PeriodModel(
        [property: JsonPropertyName("period")] int Period,
        [property: JsonPropertyName("slot")] string Slot,
        [property: JsonPropertyName("start")] string Start,
        [property: JsonPropertyName("end")] string End,
        [property: JsonPropertyName("type")] string Type);
}
