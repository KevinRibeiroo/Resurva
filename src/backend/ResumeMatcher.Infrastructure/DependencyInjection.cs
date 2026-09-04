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
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IResumeTextExtractor, PdfResumeTextExtractor>();
        services.AddScoped<IResumeTextExtractor, DocxResumeTextExtractor>();

        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        var provider = configuration["LLM:Provider"] ?? "Mock";
        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ILLMProvider, GeminiLLMProvider>();
        }
        else if (provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ILLMProvider, MockLLMProvider>();
        }
        else
        {
            throw new InvalidOperationException($"Unsupported LLM provider '{provider}'.");
        }
        return services;
    }
}
