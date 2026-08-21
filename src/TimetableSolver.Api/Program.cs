using TimetableSolver.Api.Middleware;
using TimetableSolver.Infrastructure.DependencyInjection;
using TimetableSolver.Solver.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

//  Logging 
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Timetable Solver API",
        Version = "v1",
        Description = "Full-school CP-SAT timetable generation for RDPL's timetable product assessment."
    });
});

builder.Services.AddTimetableInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddTimetableSolver(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Middleware pipeline 
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

//app.MapGet("/", () => Results.Redirect("/swagger"));
//app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow }));

app.Run();

public partial class Program
{
}
