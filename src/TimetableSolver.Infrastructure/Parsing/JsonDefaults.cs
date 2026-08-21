using System.Text.Json;

namespace TimetableSolver.Infrastructure.Parsing;

/// <summary>Single shared <see cref="JsonSerializerOptions"/> instance for every JSON repository.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
