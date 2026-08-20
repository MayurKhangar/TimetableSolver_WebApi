using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TimetableSolver.Infrastructure.Repositories;
using Xunit;

namespace TimetableSolver.Tests.Parsing;

public sealed class MarkdownTeacherAssignmentRepositoryTests
{
    private static string TestDataPath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", fileName);

    [Fact]
    public async Task GetTeacherRosterAsync_ParsesRealTeachersOnly()
    {
        var repository = new MarkdownTeacherAssignmentRepository(
            TestDataPath("TEACHER_CLASS_ASSIGNMENTS.md"), NullLogger<MarkdownTeacherAssignmentRepository>.Instance);

        var teachers = await repository.GetTeacherRosterAsync();

        teachers.Should().NotBeEmpty();
        teachers.Should().Contain(t => t.Code == "002" && t.Name == "Ruchi Singh");
        teachers.Should().NotContain(t => t.Code == "UNASSIGNED-TT");
    }

    [Fact]
    public async Task GetAssignmentRowsAsync_SeparatesPlaceholdersFromRealAssignments()
    {
        var repository = new MarkdownTeacherAssignmentRepository(
            TestDataPath("TEACHER_CLASS_ASSIGNMENTS.md"), NullLogger<MarkdownTeacherAssignmentRepository>.Instance);

        var rows = await repository.GetAssignmentRowsAsync();

        rows.Should().Contain(r => r.IsUnassignedPlaceholder);
        rows.Should().Contain(r => !r.IsUnassignedPlaceholder && r.TeacherCode == "002");

        var placeholderRow = rows.First(r => r.IsUnassignedPlaceholder);
        placeholderRow.TeacherCode.Should().Be("UNASSIGNED-TT");
    }

    [Fact]
    public async Task GetAssignmentRowsAsync_RuchiSinghTeachesArtEducationInThreeClasses()
    {
        var repository = new MarkdownTeacherAssignmentRepository(
            TestDataPath("TEACHER_CLASS_ASSIGNMENTS.md"), NullLogger<MarkdownTeacherAssignmentRepository>.Instance);

        var rows = await repository.GetAssignmentRowsAsync();

        var ruchiArtRows = rows.Where(r => r.TeacherCode == "002" && r.ItemName == "Art Education").ToList();

        ruchiArtRows.Should().HaveCount(3);
        ruchiArtRows.Select(r => r.ClassLabel).Should().BeEquivalentTo(new[] { "Class 1", "Class 2", "Class 7" });
    }
}
