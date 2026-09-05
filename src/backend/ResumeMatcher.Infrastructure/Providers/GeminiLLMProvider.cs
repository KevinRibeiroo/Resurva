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
    public const string CurrentPromptVersion = "v1";

    private readonly GeminiOptions _options;
    private readonly Client _client;
    private readonly GeminiRequestExecutor _requestExecutor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ModelName => _options.Model;
    public string ConfigurationFingerprint => FormattableString.Invariant($"temperature:{_options.Temperature}");
    public string PromptVersion => CurrentPromptVersion;

    private static string BuildSystemInstruction(string currentDate) => $"""
        Você é um avaliador técnico imparcial e rigoroso de currículos.
        Sua função é comparar o texto de um currículo com a descrição de uma vaga de emprego e retornar uma análise estruturada estritamente aderente aos fatos.

        Data de referência atual do sistema: {currentDate}.

        Diretrizes obrigatórias de segurança, cronologia e integridade:
        1. NUNCA invente ou presuma qualificações, experiências ou habilidades que não estejam explicitamente declaradas no currículo.
        2. Cronologia e datas:
           - Use a data de referência ({currentDate}) para validação temporal.
           - Experiências iniciadas até a data de referência com término 'Atual', 'Presente' ou 'Present' representam vínculos em andamento válidos, e NUNCA devem ser classificadas como futuras.
           - Não trate datas incompletas ou ausentes como inválidas ou inconsistentes se não houver prova factual explícita.
        3. Para cada habilidade encontrada (matchedSkills) e requisito atendido (requirementsMet), forneça a citação textual exata no campo 'evidence'. Se não houver citação textual exata, defina 'evidence' como null.
        4. Se uma habilidade ou requisito da vaga não constar no currículo, liste-o em missingSkills ou requirementsMissing.
        5. Senioridade como requisito mínimo:
           - A senioridade exigida pela vaga atua como requisito mínimo.
           - Se o candidato possui nível igual ou superior ao exigido pela vaga (ex.: Pleno ou Sênior para vaga Júnior), o score de senioridade (seniorityMatch) DEVE ser 100.
           - NUNCA reduza a pontuação de aderência pelo fato de o candidato ter qualificação superior à exigida.
           - Em caso de sobrequalificação, registre apenas uma observação informativa em pointsOfAttention (ex.: potencial risco de alinhamento de expectativas salariais ou de escopo), mantendo a pontuação de senioridade em 100.
        6. Anos de experiência:
           - Possuir mais anos de experiência que o solicitado pela vaga NUNCA reduz o score (experienceMatch = 100 se igual ou maior).
        7. Pontos de Atenção (pointsOfAttention):
           - Devem refletir exclusivamente lacunas reais comprovadas, sobrequalificação ou inconsistências cronológicas factuais evidentes. Nunca invente problemas.
        8. Avalie com precisão percentual de 0 a 100 a aderência para:
           - experienceMatch: compatibilidade de anos e nível prático de experiência.
           - seniorityMatch: compatibilidade do nível de senioridade como requisito mínimo (>= exigido = 100).
           - educationMatch: compatibilidade da formação acadêmica e certificações.
        9. Classificações adicionais:
           - candidateSeniority: nível de senioridade identificado no candidato (ex.: "Junior", "Pleno", "Senior", "Lead").
           - requiredSeniority: nível de senioridade solicitado pela vaga (ex.: "Junior", "Pleno", "Senior", "Lead").
           - candidateExperienceYears: total estimado de anos de experiência relevante do candidato.
           - requiredExperienceYears: anos de experiência exigidos pela vaga (se especificado).
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
            "educationMatch": { "type": "number" },
            "candidateSeniority": { "type": "string" },
            "requiredSeniority": { "type": "string" },
            "candidateExperienceYears": { "type": "number" },
            "requiredExperienceYears": { "type": "number" }
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

    public GeminiLLMProvider(IOptions<GeminiOptions> options, GeminiRequestExecutor requestExecutor)
    {
        _options = options.Value;
        _client = CreateClient(_options);
        _requestExecutor = requestExecutor;
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

        var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var userPrompt = $"""
            Considere como data de referência atual do sistema: {currentDate}.

            ### Descrição da Vaga:
            {jobDescription}

            ### Conteúdo do Currículo:
            {resumeText}
            """;

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = [new Part { Text = BuildSystemInstruction(currentDate) }]
            },
            Temperature = _options.Temperature,
            ResponseMimeType = "application/json",
            ResponseJsonSchema = JsonNode.Parse(ResponseSchemaJson)
        };

        var response = await _requestExecutor.ExecuteAsync(
            requestCancellationToken => _client.Models.GenerateContentAsync(
                model: _options.Model,
                contents: userPrompt,
                config: config,
                cancellationToken: requestCancellationToken),
            cancellationToken);

        var candidate = response.Candidates?.FirstOrDefault()
            ?? throw new LLMProviderResponseException("Gemini returned no candidates in the response.");

        var part = candidate.Content?.Parts?.FirstOrDefault()
            ?? throw new LLMProviderResponseException("Gemini returned no content parts in candidate response.");

        var rawJson = part.Text
            ?? throw new LLMProviderResponseException("Gemini response content part contained empty text.");

        StructuredComparisonModel? comparison;
        try
        {
            comparison = JsonSerializer.Deserialize<StructuredComparisonModel>(rawJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new LLMProviderResponseException("Gemini returned invalid structured output.", exception);
        }

        return comparison
            ?? throw new LLMProviderResponseException("Gemini returned empty structured output.");
    }
}
