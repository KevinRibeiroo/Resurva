using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ResumeMatcher.Application;
using ResumeMatcher.Api;
using ResumeMatcher.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddInfrastructure(builder.Configuration);

if (args.Contains("--purge-expired", StringComparer.Ordinal))
{
    await using var maintenance = builder.Build();
    await using var scope = maintenance.Services.CreateAsyncScope();
    var deleted = await scope.ServiceProvider.GetRequiredService<RetentionCleanupService>()
        .PurgeExpiredAsync(CancellationToken.None);
    maintenance.Logger.LogInformation("Retenção: {DeletedCount} registros expirados removidos", deleted);
    return;
}
if (args.Contains("--migrate-only", StringComparer.Ordinal))
{
    await using var migrationApp = builder.Build();
    await using var scope = migrationApp.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
    if (db.Database.IsRelational())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
    migrationApp.Logger.LogInformation("Database migration completed successfully.");
    return;
}
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddFirebaseAuthentication(builder.Configuration);
builder.Services.Configure<ScoringOptions>(builder.Configuration.GetSection(ScoringOptions.SectionName));
builder.Services.AddScoped<IScoringEngine, WeightedScoringEngine>();
builder.Services.AddScoped<IOptimizationSafetyValidator, OptimizationSafetyValidator>();
builder.Services.AddScoped<IResumeOptimizationService, ResumeOptimizationService>();
builder.Services.AddScoped<IResumeService, ResumeService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
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
    options.AddPolicy(ApiRateLimitOptions.PolicyName, httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitSettings.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimitSettings.WindowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "0");
    if (context.Request.IsHttps)
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }
    await next();
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate", true);
if (autoMigrate)
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
    }
}

app.Run();

public partial class Program;
