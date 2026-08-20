namespace TimetableSolver.Infrastructure.Parsing;

public static class MarkdownTableParser
{
    public static string[] SplitRow(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|'))
        {
            return Array.Empty<string>();
        }

        var cells = trimmed
            .Trim('|')
            .Split('|')
            .Select(c => c.Trim())
            .ToArray();

        var isSeparatorRow = cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':'));
        return isSeparatorRow ? Array.Empty<string>() : cells;
    }

    public static bool IsTableRow(string line) => line.Trim().StartsWith('|');

    public static int ParseInt(string cell)
    {
        var cleaned = cell.Replace("*", string.Empty).Trim();
        return int.TryParse(cleaned, out var value) ? value : 0;
    }
}
