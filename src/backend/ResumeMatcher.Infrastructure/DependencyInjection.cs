using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ResumeMatcher")
            ?? throw new InvalidOperationException("Connection string 'ResumeMatcher' is required.");
        services.AddDbContext<ResumeMatcherDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<RetentionCleanupService>();
        services.AddScoped<IAccountDataService, AccountDataService>();
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IResumeOptimizationRepository, ResumeOptimizationRepository>();
        services.AddScoped<IResumeTextExtractor, PdfResumeTextExtractor>();
        services.AddScoped<IResumeTextExtractor, DocxResumeTextExtractor>();
        services.AddScoped<IResumeDocumentExporter, DocxResumeDocumentExporter>();
        services.AddScoped<IResumeDocumentExporter, PdfResumeDocumentExporter>();
        services.AddScoped<ILayoutPreservingExportService, LayoutPreservingExportService>();

        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider), "LLM:Provider is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Model), "LLM:Model is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Location), "LLM:Location is required.")
            .Validate(options => options.Temperature is >= 0 and <= 2, "LLM:Temperature must be between 0 and 2.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 300, "LLM:TimeoutSeconds must be between 1 and 300.")
            .Validate(options => options.MaxRetries is >= 0 and <= 3, "LLM:MaxRetries must be between 0 and 3.")
            .Validate(options => options.RetryBaseDelayMilliseconds is >= 100 and <= 5_000,
                "LLM:RetryBaseDelayMilliseconds must be between 100 and 5000.")
            .ValidateOnStart();
        services.AddSingleton<GeminiRequestExecutor>();

        var provider = configuration["LLM:Provider"] ?? "Mock";
        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ILLMProvider, GeminiLLMProvider>();
            services.AddScoped<IResumeOptimizationProvider, GeminiResumeOptimizationProvider>();
        }
        else if (provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ILLMProvider, MockLLMProvider>();
            services.AddScoped<IResumeOptimizationProvider, MockResumeOptimizationProvider>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported LLM provider '{provider}'.");
        }
        return services;
    }
}
