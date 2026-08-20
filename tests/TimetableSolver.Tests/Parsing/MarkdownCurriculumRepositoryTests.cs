using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TimetableSolver.Domain.Enums;
using TimetableSolver.Infrastructure.Repositories;
using Xunit;

namespace TimetableSolver.Tests.Parsing;

public sealed class MarkdownCurriculumRepositoryTests
{
    private static string TestDataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    [Fact]
    public async Task GetCurriculumAsync_ParsesAllTwelveClasses()
    {
        var repository = new MarkdownCurriculumRepository(TestDataPath("CLASS_WISE_SUBJECTS.md"), NullLogger<MarkdownCurriculumRepository>.Instance);

        var rows = await repository.GetCurriculumAsync();

        var classKeys = rows.Select(r => r.CurriculumKey).Distinct().ToList();
        classKeys.Should().HaveCount(12);
        classKeys.Should().Contain("Class 1");
        classKeys.Should().Contain("Class 12");
    }

    [Fact]
    public async Task GetCurriculumAsync_EachClassSumsToFiftyTwoPeriodsPerWeek()
    {
        var repository = new MarkdownCurriculumRepository(TestDataPath("CLASS_WISE_SUBJECTS.md"), NullLogger<MarkdownCurriculumRepository>.Instance);
        var rows = await repository.GetCurriculumAsync();

        foreach (var group in rows.GroupBy(r => r.CurriculumKey))
        {
            group.Sum(r => r.PeriodsPerWeek).Should().Be(52, $"{group.Key} must total 52 periods/week per CLASS_WISE_SUBJECTS.md");
        }
    }

    [Fact]
    public async Task GetCurriculumAsync_ClassifiesActivityGroupsCorrectly()
    {
        var repository = new MarkdownCurriculumRepository(TestDataPath("CLASS_WISE_SUBJECTS.md"), NullLogger<MarkdownCurriculumRepository>.Instance);
        var rows = await repository.GetCurriculumAsync();

        var class1GamesLibrary = rows.Single(r => r.CurriculumKey == "Class 1" && r.Name == "Games / Library");
        class1GamesLibrary.Type.Should().Be(CurriculumItemType.Activity);

        var class1Mathematics = rows.Single(r => r.CurriculumKey == "Class 1" && r.Name == "Mathematics");
        class1Mathematics.Type.Should().Be(CurriculumItemType.Subject);
    }
}
