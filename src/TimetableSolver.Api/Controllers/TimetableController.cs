using Microsoft.AspNetCore.Mvc;
using TimetableSolver.Application.Dtos;
using TimetableSolver.Application.Interfaces;
using TimetableSolver.Application.Mapping;
using TimetableSolver.Application.Options;

namespace TimetableSolver.Api.Controllers;

[ApiController]
[Route("api/timetable")]
public sealed class TimetableController : ControllerBase
{
    private readonly ISchoolDataService _schoolDataService;
    private readonly ITimetableGenerationService _generationService;
    private readonly ITimetableStateStore _stateStore;
    private readonly DataSourceOptions _dataSourceOptions;
    private readonly ILogger<TimetableController> _logger;

    public TimetableController(
        ISchoolDataService schoolDataService,
        ITimetableGenerationService generationService,
        ITimetableStateStore stateStore,
        Microsoft.Extensions.Options.IOptions<DataSourceOptions> dataSourceOptions,
        ILogger<TimetableController> logger)
    {
        _schoolDataService = schoolDataService;
        _generationService = generationService;
        _stateStore = stateStore;
        _dataSourceOptions = dataSourceOptions.Value;
        _logger = logger;
    }

    [HttpPost("load-data")]
    [ProducesResponseType(typeof(LoadDataResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoadDataResponse>> LoadData(CancellationToken cancellationToken)
    {
        var school = await _schoolDataService.LoadSchoolModelAsync(cancellationToken);
        _stateStore.SetSchool(school);

        _logger.LogInformation("Loaded school model: {Sections} sections, {Conflicts} data conflicts",
            school.Sections.Count, school.DataConflicts.Count);

        return Ok(school.ToLoadDataResponse(_dataSourceOptions.Mode));
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerateResponse>> Generate(CancellationToken cancellationToken)
    {
        var school = _stateStore.CurrentSchool ?? await _schoolDataService.LoadSchoolModelAsync(cancellationToken);
        _stateStore.SetSchool(school);

        var result = await _generationService.GenerateAsync(school, cancellationToken);
        _stateStore.SetGenerationResult(result);

        _logger.LogInformation("Generation finished: status={Status}, sectionsScheduled={Scheduled}/{Total}",
            result.Status, result.SectionsScheduled, result.SectionsTotal);

        return Ok(result.ToGenerateResponse());
    }

    [HttpGet("sections")]
    [ProducesResponseType(typeof(GenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GenerateResponse> GetSections()
    {
        var result = _stateStore.LastGenerationResult;
        if (result is null)
        {
            return NotFound(new { message = "No timetable has been generated yet. Call POST /api/timetable/generate first." });
        }

        return Ok(result.ToGenerateResponse());
    }

    [HttpGet("sections/{sectionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetSection(string sectionId)
    {
        var result = _stateStore.LastGenerationResult;
        if (result is null)
        {
            return NotFound(new { message = "No timetable has been generated yet. Call POST /api/timetable/generate first." });
        }

        var lessons = result.Lessons.Where(l => string.Equals(l.SectionId, sectionId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (lessons.Count == 0)
        {
            return NotFound(new { message = $"No scheduled lessons found for section '{sectionId}'." });
        }

        var byDay = lessons
            .GroupBy(l => l.Day.ToString().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Period)
                .Select(l => new LessonDto(l.Period, l.ItemName, l.TeacherCode, l.TeacherName)));

        return Ok(new { sectionId, sectionDisplayName = lessons[0].SectionDisplayName, timetable = byDay });
    }

    [HttpGet("teachers")]
    [ProducesResponseType(typeof(GenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetTeachers()
    {
        var result = _stateStore.LastGenerationResult;
        if (result is null)
        {
            return NotFound(new { message = "No timetable has been generated yet. Call POST /api/timetable/generate first." });
        }

        return Ok(result.ToGenerateResponse().TeacherTimetables);
    }

    [HttpGet("conflicts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetConflicts()
    {
        var school = _stateStore.CurrentSchool;
        var result = _stateStore.LastGenerationResult;

        return Ok(new
        {
            dataConflicts = school?.DataConflicts.Select(c => c.ToDto()).ToList() ?? new List<DataConflictDto>(),
            schedulingConflicts = result?.SchedulingConflicts ?? Array.Empty<string>()
        });
    }
}
