using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Exceptions;
using TimetableSolver.Application.Interfaces;

namespace TimetableSolver.Infrastructure.Repositories;

public sealed class JsonSectionRepository : ISectionRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonSectionRepository> _logger;

    public JsonSectionRepository(string filePath, ILogger<JsonSectionRepository> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RawSectionRecord>> GetSectionsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new DataLoadException($"Sections file not found: {_filePath}", _filePath);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync<SectionsFileModel>(stream, JsonOptions, cancellationToken)
                ?? throw new DataLoadException("sections.json deserialized to null.", _filePath);

            var records = document.Sections
                .Select(s => new RawSectionRecord(s.Id, s.DisplayName, s.Grade, s.Section, s.CurriculumKey, s.Capacity))
                .ToList();

            _logger.LogInformation("Parsed {Count} sections from {File}", records.Count, Path.GetFileName(_filePath));
            return records;
        }
        catch (JsonException ex)
        {
            throw new DataLoadException($"sections.json is not valid JSON: {ex.Message}", _filePath, ex);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record SectionsFileModel(
        [property: JsonPropertyName("sections")] List<SectionRecordModel> Sections);

    private sealed record SectionRecordModel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("grade")] string Grade,
        [property: JsonPropertyName("section")] string Section,
        [property: JsonPropertyName("curriculumKey")] string CurriculumKey,
        [property: JsonPropertyName("capacity")] int Capacity);
}
