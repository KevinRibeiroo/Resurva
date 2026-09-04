using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

public sealed class GeminiLLMProvider : ILLMProvider
{
    private readonly GeminiOptions _options;
    private readonly Client _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SystemInstructionText = """
        Você é um avaliador técnico imparcial e rigoroso de currículos.
        Sua função é comparar o texto de um currículo com a descrição de uma vaga de emprego e retornar uma análise estruturada estritamente aderente aos fatos.

        Diretrizes obrigatórias de segurança e integridade:
        1. NUNCA invente ou presuma qualificações, experiências ou habilidades que não estejam explicitamente declaradas no currículo.
        2. Para cada habilidade encontrada (matchedSkills) e requisito atendido (requirementsMet), forneça a citação exata ou evidência textual encontrada no currículo.
        3. Se uma habilidade ou requisito da vaga não constar no currículo, liste-o em missingSkills ou requirementsMissing.
        4. Avalie com precisão percentual de 0 a 100 a aderência para:
           - experienceMatch: compatibilidade de anos e nível prático de experiência.
           - seniorityMatch: compatibilidade do nível de senioridade (ex: Júnior, Pleno, Sênior, Especialista).
           - educationMatch: compatibilidade da formação acadêmica e certificações.
        5. Destaque pontos fortes reais (strengths), pontos de atenção (pointsOfAttention) e recomendações responsáveis (recommendations).
        """;

    private const string ResponseSchemaJson = """
        {
          "type": "object",
          "properties": {
            "matchedSkills": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "missingSkills": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "requirementsMet": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "requirementsMissing": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "strengths": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "pointsOfAttention": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "recommendations": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "text": { "type": "string" },
                  "evidence": { "type": "string" }
                },
                "required": ["text"]
              }
            },
            "experienceMatch": { "type": "number" },
            "seniorityMatch": { "type": "number" },
            "educationMatch": { "type": "number" }
          },
          "required": [
            "matchedSkills",
            "missingSkills",
            "requirementsMet",
            "requirementsMissing",
            "strengths",
            "pointsOfAttention",
            "recommendations",
            "experienceMatch",
            "seniorityMatch",
            "educationMatch"
          ]
        }
        """;

    public GeminiLLMProvider(IOptions<GeminiOptions> options)
    {
        _options = options.Value;
        _client = CreateClient(_options);
    }

    private static Client CreateClient(GeminiOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new Client(apiKey: options.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(options.ProjectId))
        {
            return new Client(project: options.ProjectId, location: options.Location, enterprise: true);
        }

        return new Client();
    }

    public async Task<StructuredComparisonModel> CompareAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userPrompt = $"""
            ### Descrição da Vaga:
            {jobDescription}

            ### Conteúdo do Currículo:
            {resumeText}
            """;

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = [new Part { Text = SystemInstructionText }]
            },
            Temperature = _options.Temperature,
            ResponseMimeType = "application/json",
            ResponseJsonSchema = JsonNode.Parse(ResponseSchemaJson)
        };

        var response = await _client.Models.GenerateContentAsync(
            model: _options.Model,
            contents: userPrompt,
            config: config,
            cancellationToken: cancellationToken);

        var candidate = response.Candidates?.FirstOrDefault()
            ?? throw new InvalidOperationException("Gemini returned no candidates in the response.");

        var part = candidate.Content?.Parts?.FirstOrDefault()
            ?? throw new InvalidOperationException("Gemini returned no content parts in candidate response.");

        var rawJson = part.Text
            ?? throw new InvalidOperationException("Gemini response content part contained empty text.");

        var comparison = JsonSerializer.Deserialize<StructuredComparisonModel>(rawJson, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Gemini structured output into StructuredComparisonModel.");

        return comparison;
    }
}
