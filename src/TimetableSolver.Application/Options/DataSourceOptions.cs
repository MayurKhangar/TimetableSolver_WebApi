namespace TimetableSolver.Application.Options;

public sealed class DataSourceOptions
{
    public const string SectionName = "DataSource";

    public string Mode { get; set; } = "FullDataset";

    public string BasePath { get; set; } = "Data";

    public string SectionsFile { get; set; } = "sections.json";
    public string BellScheduleFile { get; set; } = "bell-schedule.json";
    public string ClassWiseSubjectsFile { get; set; } = "CLASS_WISE_SUBJECTS.md";
    public string TeacherClassAssignmentsFile { get; set; } = "TEACHER_CLASS_ASSIGNMENTS.md";
    public string SampleSchoolFile { get; set; } = "school-sample.json";
}
