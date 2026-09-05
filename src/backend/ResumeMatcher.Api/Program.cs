using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Api;
using ResumeMatcher.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.Configure<ScoringOptions>(builder.Configuration.GetSection(ScoringOptions.SectionName));
builder.Services.AddScoped<IScoringEngine, WeightedScoringEngine>();
builder.Services.AddScoped<IResumeService, ResumeService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddDbContextCheck<ResumeMatcherDbContext>("postgresql");

var rateLimitSettings = builder.Configuration
    .GetSection(ApiRateLimitOptions.SectionName)
    .Get<ApiRateLimitOptions>() ?? new ApiRateLimitOptions();
builder.Services.AddOptions<ApiRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(ApiRateLimitOptions.SectionName))
    .Validate(options => options.PermitLimit is >= 1 and <= 1_000,
        "RateLimiting:PermitLimit must be between 1 and 1000.")
    .Validate(options => options.WindowSeconds is >= 1 and <= 3_600,
        "RateLimiting:WindowSeconds must be between 1 and 3600.")
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(ApiRateLimitOptions.PolicyName, limiter =>
    {
        limiter.PermitLimit = rateLimitSettings.PermitLimit;
        limiter.Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds);
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program;
