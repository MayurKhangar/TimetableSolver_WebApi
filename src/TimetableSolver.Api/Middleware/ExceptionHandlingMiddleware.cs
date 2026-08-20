using System.Net;
using System.Text.Json;
using TimetableSolver.Application.Exceptions;

namespace TimetableSolver.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DataLoadException ex)
        {
            _logger.LogWarning(ex, "Data load failure for file {FileName}", ex.FileName);
            await WriteErrorAsync(context, HttpStatusCode.UnprocessableEntity, "DATA_LOAD_ERROR", ex.Message);
        }
        catch (TimetableGenerationException ex)
        {
            _logger.LogError(ex, "Timetable generation failure");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "GENERATION_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "UNEXPECTED_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string code, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = JsonSerializer.Serialize(new { success = false, error = new { code, message } });
        await context.Response.WriteAsync(payload);
    }
}
