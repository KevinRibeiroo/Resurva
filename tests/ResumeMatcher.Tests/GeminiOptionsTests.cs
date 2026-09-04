using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Application;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class GeminiOptionsTests
{
    [Fact]
    public void GeminiOptions_HasExpectedDefaults()
    {
        var options = new GeminiOptions();

        Assert.Equal("Mock", options.Provider);
        Assert.Equal("gemini-3.5-flash", options.Model);
        Assert.Null(options.ApiKey);
        Assert.Null(options.ProjectId);
        Assert.Equal("us-central1", options.Location);
        Assert.Equal(0.1, options.Temperature);
    }

    [Fact]
    public void AddInfrastructure_WithGeminiProvider_RegistersGeminiLLMProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ResumeMatcher"] = "Host=localhost;Database=resumematcher_test;Username=postgres",
                ["LLM:Provider"] = "Gemini",
                ["LLM:ApiKey"] = "test-api-key",
                ["LLM:Model"] = "gemini-2.5-flash"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        var llmProvider = provider.GetRequiredService<ILLMProvider>();
        Assert.IsType<GeminiLLMProvider>(llmProvider);
    }

    [Fact]
    public void AddInfrastructure_WithMockProvider_RegistersMockLLMProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ResumeMatcher"] = "Host=localhost;Database=resumematcher_test;Username=postgres",
                ["LLM:Provider"] = "Mock"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();

        var llmProvider = provider.GetRequiredService<ILLMProvider>();
        Assert.IsType<MockLLMProvider>(llmProvider);
    }

    [Fact]
    public void StructuredComparisonModel_DeserializesCorrectly_FromGeminiJsonOutput()
    {
        var sampleGeminiJson = """
        {
          "matchedSkills": [
            { "text": "C#", "evidence": "Experiência de 5 anos com C#" },
            { "text": "ASP.NET Core", "evidence": "Desenvolvimento de APIs com ASP.NET Core" }
          ],
          "missingSkills": [
            { "text": "Docker", "evidence": null }
          ],
          "requirementsMet": [
            { "text": "Graduação em Ciência da Computação", "evidence": "Bacharel em Ciência da Computação" }
          ],
          "requirementsMissing": [],
          "strengths": [
            { "text": "Forte experiência em backend .NET" }
          ],
          "pointsOfAttention": [
            { "text": "Sem menção a containers ou Docker" }
          ],
          "recommendations": [
            { "text": "Destacar se possui experiência prática com Docker" }
          ],
          "experienceMatch": 90.0,
          "seniorityMatch": 85.0,
          "educationMatch": 100.0
        }
        """;

        var result = System.Text.Json.JsonSerializer.Deserialize<StructuredComparisonModel>(
            sampleGeminiJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal(2, result.MatchedSkills.Count);
        Assert.Equal("C#", result.MatchedSkills[0].Text);
        Assert.Equal("Experiência de 5 anos com C#", result.MatchedSkills[0].Evidence);
        Assert.Single(result.MissingSkills);
        Assert.Equal(90.0, result.ExperienceMatch);
        Assert.Equal(85.0, result.SeniorityMatch);
        Assert.Equal(100.0, result.EducationMatch);
    }
}
