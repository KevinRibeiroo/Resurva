using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ResumeMatcherDbContext>(options => options.UseSqlite(configuration.GetConnectionString("ResumeMatcher") ?? "Data Source=resumematcher.db"));
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IResumeTextExtractor, PdfResumeTextExtractor>();
        services.AddScoped<IResumeTextExtractor, DocxResumeTextExtractor>();

        var provider = configuration["LLM:Provider"] ?? "Mock";
        if (!provider.Equals("Mock", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported LLM provider '{provider}'.");
        services.AddScoped<ILLMProvider, MockLLMProvider>();
        return services;
    }
}
