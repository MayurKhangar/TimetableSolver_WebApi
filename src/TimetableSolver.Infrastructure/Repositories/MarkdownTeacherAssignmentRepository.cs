using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TimetableSolver.Application.Exceptions;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Domain.Entities;
using TimetableSolver.Infrastructure.Parsing;

namespace TimetableSolver.Infrastructure.Repositories;

public sealed class MarkdownTeacherAssignmentRepository : ITeacherAssignmentRepository
{
    private const string UnassignedCode = "UNASSIGNED-TT";

    private static readonly Regex TeacherHeadingPattern =
        new(@"^###\s+`([^`]+)`\s*[—-]\s*(.+)$", RegexOptions.Compiled);

    private static readonly Regex RosterCodePattern = new(@"^`([^`]+)`$", RegexOptions.Compiled);

    private readonly string _filePath;
    private readonly ILogger<MarkdownTeacherAssignmentRepository> _logger;

    public MarkdownTeacherAssignmentRepository(string filePath, ILogger<MarkdownTeacherAssignmentRepository> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Teacher>> GetTeacherRosterAsync(CancellationToken cancellationToken = default)
    {
        var lines = await ReadLinesAsync(cancellationToken);
        var teachers = new List<Teacher>();
        var inRosterTable = false;
        var headerSeen = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("## 2."))
            {
                inRosterTable = true;
                headerSeen = false;
                continue;
            }

            if (inRosterTable && line.TrimStart().StartsWith("## "))
            {
                break; 
            }

            if (!inRosterTable || !MarkdownTableParser.IsTableRow(line))
            {
                continue;
            }

            var cells = MarkdownTableParser.SplitRow(line);
            if (cells.Length < 6)
            {
                continue; 
            }

            if (!headerSeen)
            {
                headerSeen = true;
                continue;
            }

            var codeMatch = RosterCodePattern.Match(cells[1].Trim());
            var code = codeMatch.Success ? codeMatch.Groups[1].Value : cells[1].Trim();

            teachers.Add(new Teacher
            {
                Id = NormalizeTeacherId(code),
                Code = code,
                Name = cells[2].Trim()
            });
        }

        _logger.LogInformation("Parsed {Count} teachers from roster in {File}", teachers.Count, Path.GetFileName(_filePath));
        return teachers;
    }

    public async Task<IReadOnlyList<RawAssignmentRow>> GetAssignmentRowsAsync(CancellationToken cancellationToken = default)
    {
        var lines = await ReadLinesAsync(cancellationToken);
        var rows = new List<RawAssignmentRow>();

        var mode = ParseMode.None;
        string? currentTeacherCode = null;
        string? currentTeacherName = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();

            if (trimmedLine.StartsWith("## 3."))
            {
                mode = ParseMode.Placeholders;
                continue;
            }

            if (trimmedLine.StartsWith("## 4."))
            {
                mode = ParseMode.Assignments;
                continue;
            }

            if (trimmedLine.StartsWith("## ") && mode is ParseMode.Placeholders or ParseMode.Assignments)
            {
                mode = ParseMode.None;
                continue;
            }

            if (mode == ParseMode.Assignments)
            {
                var headingMatch = TeacherHeadingPattern.Match(line);
                if (headingMatch.Success)
                {
                    currentTeacherCode = headingMatch.Groups[1].Value.Trim();
                    currentTeacherName = headingMatch.Groups[2].Value.Trim();
                    continue;
                }
            }

            if (!MarkdownTableParser.IsTableRow(line))
            {
                continue;
            }

            var cells = MarkdownTableParser.SplitRow(line);

            switch (mode)
            {
                case ParseMode.Placeholders when cells.Length >= 3 && IsClassLabel(cells[0]):
                    rows.Add(new RawAssignmentRow(
                        TeacherCode: UnassignedCode,
                        TeacherName: "Unassigned Teacher",
                        ClassLabel: cells[0].Trim(),
                        ItemName: cells[2].Trim(),
                        PeriodsPerWeekForClass: 0,
                        IsUnassignedPlaceholder: true));
                    break;

                case ParseMode.Assignments when cells.Length >= 4 && IsClassLabel(cells[0]) && currentTeacherCode is not null:
                    rows.Add(new RawAssignmentRow(
                        TeacherCode: currentTeacherCode,
                        TeacherName: currentTeacherName ?? currentTeacherCode,
                        ClassLabel: cells[0].Trim(),
                        ItemName: cells[2].Trim(),
                        PeriodsPerWeekForClass: MarkdownTableParser.ParseInt(cells[3]),
                        IsUnassignedPlaceholder: false));
                    break;
            }
        }

        if (rows.Count == 0)
        {
            throw new DataLoadException("No assignment rows parsed from TEACHER_CLASS_ASSIGNMENTS.md - check file format.", _filePath);
        }

        _logger.LogInformation(
            "Parsed {Total} assignment rows ({Placeholder} unassigned placeholders) from {File}",
            rows.Count, rows.Count(r => r.IsUnassignedPlaceholder), Path.GetFileName(_filePath));

        return rows;
    }

    private static bool IsClassLabel(string cell) => cell.Trim().StartsWith("Class ", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTeacherId(string code) => $"teacher-{code.Trim().ToLowerInvariant()}";

    private async Task<string[]> ReadLinesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            throw new DataLoadException($"Teacher assignment file not found: {_filePath}", _filePath);
        }

        return await File.ReadAllLinesAsync(_filePath, cancellationToken);
    }

    private enum ParseMode
    {
        None,
        Placeholders,
        Assignments
    }
}
