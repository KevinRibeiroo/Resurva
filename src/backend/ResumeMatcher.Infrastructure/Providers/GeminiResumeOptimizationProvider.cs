using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

public sealed class GeminiResumeOptimizationProvider : IResumeOptimizationProvider
{
    public const string CurrentPromptVersion = "v1-opt";

    private readonly GeminiOptions _options;
    private readonly Client _client;
    private readonly GeminiRequestExecutor _requestExecutor;
    private readonly ILogger<GeminiResumeOptimizationProvider> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ModelName => _options.Model;
    public string ConfigurationFingerprint => FormattableString.Invariant($"temperature:{_options.Temperature}");
    public string PromptVersion => CurrentPromptVersion;

    private static string BuildSystemInstruction() => """
        Você é um assistente técnico especialista em redação e otimização de currículos para processos seletivos.
        Sua função é gerar sugestões práticas de melhoria e adaptação textual para alinhar o currículo à descrição da vaga, respeitando estritamente a verdade e os fatos.

        Diretrizes mandatórias de integridade e segurança:
        1. Classificação das sugestões (campo 'level'):
           - 'Safe': Melhorias de redação, síntese, clareza ou ênfase para competências e realizações que JÁ ESTÃO COMPROVADAS no currículo. Para sugestões 'Safe', você DEVE fornecer em 'evidence' o trecho textual exato do currículo que comprova o fato. Se for uma melhoria de um trecho específico, forneça-o em 'originalText'.
           - 'NeedsConfirmation': Inclusão de competências, ferramentas, palavras-chave ou requisitos exigidos pela vaga que NÃO foram citados no currículo. NUNCA assuma que o candidato possui tais itens. Você DEVE formular uma pergunta direta em 'confirmationQuestion' para que o candidato confirme se realmente possui tal vivência antes que o texto possa ser incorporado.
           - 'Forbidden': Sugestões que inventam qualificações, diplomas, certificações, escopos de liderança sênior/executiva ou realizações irreais sem embasamento. Devem ser sinalizadas para alertar sobre o que NÃO fazer.
        2. Toda sugestão DEVE conter:
           - 'level': "Safe", "NeedsConfirmation" ou "Forbidden".
           - 'originalText': trecho original a ser substituído/melhorado (ou string vazia se for adição).
           - 'proposedText': texto pronto para inclusão ou substituição.
           - 'reason': justificativa clara explicando por que a alteração favorece a aderência à vaga.
           - 'evidence': citação textual exata do currículo para sugestões 'Safe' (ou null/vazio se não houver).
           - 'confirmationQuestion': pergunta formulada quando 'level' for 'NeedsConfirmation' (ou null se 'Safe').
        """;

    private const string ResponseSchemaJson = """
        {
          "type": "object",
          "properties": {
            "suggestions": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "level": { "type": "string", "enum": ["Safe", "NeedsConfirmation", "Forbidden"] },
                  "originalText": { "type": "string" },
                  "proposedText": { "type": "string" },
                  "reason": { "type": "string" },
                  "evidence": { "type": "string" },
                  "confirmationQuestion": { "type": "string" }
                },
                "required": ["level", "proposedText", "reason"]
              }
            }
          },
          "required": ["suggestions"]
        }
        """;

    public GeminiResumeOptimizationProvider(
        IOptions<GeminiOptions> options,
        GeminiRequestExecutor requestExecutor,
        ILogger<GeminiResumeOptimizationProvider>? logger = null)
    {
        _options = options.Value;
        _client = CreateClient(_options);
        _requestExecutor = requestExecutor;
        _logger = logger ?? NullLogger<GeminiResumeOptimizationProvider>.Instance;
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

    public async Task<IReadOnlyList<OptimizationSuggestionModel>> GenerateSuggestionsAsync(
        string resumeText,
        string jobDescription,
        StructuredComparisonModel comparisonContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matchedSummary = string.Join("; ", comparisonContext.MatchedSkills.Select(s => s.Text));
        var missingSummary = string.Join("; ", comparisonContext.MissingSkills.Select(s => s.Text));

        var userPrompt = $"""
            ### Descrição da Vaga:
            {jobDescription}

            ### Conteúdo do Currículo:
            {resumeText}

            ### Contexto da Análise Prévia:
            - Habilidades Encontradas: {matchedSummary}
            - Lacunas Identificadas: {missingSummary}
            """;

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = [new Part { Text = BuildSystemInstruction() }]
            },
            Temperature = _options.Temperature,
            ResponseMimeType = "application/json",
            ResponseJsonSchema = JsonNode.Parse(ResponseSchemaJson)
        };

        _logger.LogInformation(
            "Chamando Gemini para gerar sugestões de otimização de currículo. Modelo: {Model}, PromptVersion: {PromptVersion}",
            _options.Model, PromptVersion);

        var stopwatch = Stopwatch.StartNew();
        var response = await _requestExecutor.ExecuteAsync(
            requestCancellationToken => _client.Models.GenerateContentAsync(
                model: _options.Model,
                contents: userPrompt,
                config: config,
                cancellationToken: requestCancellationToken),
            cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "Resposta de otimização recebida em {ElapsedMs}ms",
            stopwatch.ElapsedMilliseconds);

        var candidate = response.Candidates?.FirstOrDefault()
            ?? throw new LLMProviderResponseException("Gemini returned no candidates in the optimization response.");

        var part = candidate.Content?.Parts?.FirstOrDefault()
            ?? throw new LLMProviderResponseException("Gemini returned no content parts in candidate optimization response.");

        var rawJson = part.Text
            ?? throw new LLMProviderResponseException("Gemini optimization response content part contained empty text.");

        GeminiOptimizationResponseDto? parsedDto;
        try
        {
            parsedDto = JsonSerializer.Deserialize<GeminiOptimizationResponseDto>(rawJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new LLMProviderResponseException("Gemini returned invalid JSON for optimization suggestions.", exception);
        }

        if (parsedDto?.Suggestions == null)
        {
            return Array.Empty<OptimizationSuggestionModel>();
        }

        var result = new List<OptimizationSuggestionModel>();
        foreach (var item in parsedDto.Suggestions)
        {
            if (string.IsNullOrWhiteSpace(item.ProposedText))
                continue;

            var level = item.Level?.Trim().ToLowerInvariant() switch
            {
                "safe" => OptimizationSafetyLevel.Safe,
                "needsconfirmation" => OptimizationSafetyLevel.NeedsConfirmation,
                "forbidden" => OptimizationSafetyLevel.Forbidden,
                _ => OptimizationSafetyLevel.NeedsConfirmation
            };

            result.Add(new OptimizationSuggestionModel(
                Id: Guid.NewGuid(),
                Level: level,
                Confirmed: false,
                OriginalText: item.OriginalText ?? string.Empty,
                ProposedText: item.ProposedText.Trim(),
                Reason: item.Reason ?? string.Empty,
                Evidence: item.Evidence,
                ConfirmationQuestion: item.ConfirmationQuestion));
        }

        return result;
    }

    private sealed class GeminiOptimizationResponseDto
    {
        public List<GeminiOptimizationSuggestionDto>? Suggestions { get; set; }
    }

    private sealed class GeminiOptimizationSuggestionDto
    {
        public string? Level { get; set; }
        public string? OriginalText { get; set; }
        public string? ProposedText { get; set; }
        public string? Reason { get; set; }
        public string? Evidence { get; set; }
        public string? ConfirmationQuestion { get; set; }
    }
}
