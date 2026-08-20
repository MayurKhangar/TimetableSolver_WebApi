using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Exceptions;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Enums;
using TimetableSolver.Infrastructure.Parsing;

namespace TimetableSolver.Infrastructure.Repositories;

public sealed class MarkdownCurriculumRepository : ICurriculumRepository
{
    private static readonly Regex ClassHeadingPattern = new(@"^###\s+(Class\s+\d+)\s*\[", RegexOptions.Compiled);

    private readonly string _filePath;
    private readonly ILogger<MarkdownCurriculumRepository> _logger;

    public MarkdownCurriculumRepository(string filePath, ILogger<MarkdownCurriculumRepository> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RawCurriculumRow>> GetCurriculumAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            throw new DataLoadException($"Curriculum file not found: {_filePath}", _filePath);
        }

        var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken);
        var rows = new List<RawCurriculumRow>();
        string? currentCurriculumKey = null;
        var headerSeen = false;

        foreach (var line in lines)
        {
            var headingMatch = ClassHeadingPattern.Match(line);
            if (headingMatch.Success)
            {
                currentCurriculumKey = headingMatch.Groups[1].Value.Trim();
                headerSeen = false;
                continue;
            }

            if (currentCurriculumKey is null || !MarkdownTableParser.IsTableRow(line))
            {
                continue;
            }

            var cells = MarkdownTableParser.SplitRow(line);
            if (cells.Length == 0)
            {
                continue; // separator row
            }

            // First real row under a heading is the column header ("Type | Subject / Activity | ...").
            if (!headerSeen)
            {
                headerSeen = true;
                continue;
            }

            if (cells.Length < 4)
            {
                _logger.LogWarning("Skipping malformed curriculum row under {CurriculumKey}: {Line}", currentCurriculumKey, line);
                continue;
            }

            var type = cells[0].Trim().Equals("Activity Group", StringComparison.OrdinalIgnoreCase)
                ? CurriculumItemType.Activity
                : CurriculumItemType.Subject;

            rows.Add(new RawCurriculumRow(
                CurriculumKey: currentCurriculumKey,
                Name: cells[1].Trim(),
                Type: type,
                PeriodsPerWeek: MarkdownTableParser.ParseInt(cells[3]),
                PeriodsPerDay: MarkdownTableParser.ParseInt(cells[2])));
        }

        if (rows.Count == 0)
        {
            throw new DataLoadException("No curriculum rows parsed from CLASS_WISE_SUBJECTS.md - check file format.", _filePath);
        }

        _logger.LogInformation("Parsed {Count} curriculum rows across {ClassCount} classes from {File}",
            rows.Count, rows.Select(r => r.CurriculumKey).Distinct().Count(), Path.GetFileName(_filePath));

        return rows;
    }
}
