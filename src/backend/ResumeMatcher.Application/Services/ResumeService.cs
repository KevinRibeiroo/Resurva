using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public sealed class ResumeService(
    IEnumerable<IResumeTextExtractor> extractors,
    IResumeRepository repository,
    ICurrentUser currentUser,
    ILogger<ResumeService>? logger = null) : IResumeService
{
    private readonly ILogger<ResumeService> _logger = logger ?? NullLogger<ResumeService>.Instance;
    private const long MaxFileSize = 10 * 1024 * 1024;

    public async Task<UploadResumeResultModel> UploadAsync(UploadResumeCommand command, CancellationToken cancellationToken)
    {
        var ownerUserId = currentUser.UserId;
        _logger.LogInformation("Iniciando processamento de upload do currículo");

        if (!command.Content.CanRead)
            throw new InvalidResumeException("The uploaded file cannot be read.");
        if (command.Content.CanSeek && command.Content.Length > MaxFileSize)
            throw new InvalidResumeException("The file exceeds the 10 MB limit.");

        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        var extractor = extractors.FirstOrDefault(x => x.CanExtract(extension, command.ContentType))
            ?? throw new UnsupportedResumeFormatException("Only PDF and DOCX files are supported.");

        _logger.LogInformation("Extraindo texto usando extrator {Extractor}", extractor.GetType().Name);

        var text = (await extractor.ExtractAsync(command.Content, cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidResumeException("No text could be extracted from the resume.");
        if (text.Length > AnalysisConstraints.MaxExtractedResumeTextLength)
            throw new InvalidResumeException($"The extracted resume text exceeds the {AnalysisConstraints.MaxExtractedResumeTextLength} character limit.");

        _logger.LogInformation("Texto extraído ({CharCount} caracteres). Persistindo entidade", text.Length);

        var resume = new ResumeEntity { OwnerUserId = ownerUserId, FileName = Path.GetFileName(command.FileName), ContentType = command.ContentType, ExtractedText = text };
        await repository.AddAsync(resume, cancellationToken);

        _logger.LogInformation("Currículo persistido com ID {ResumeId}", resume.Id);
        return new(resume.Id, resume.FileName, text.Length);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Resume id is required.");

        _logger.LogInformation("Iniciando exclusão do currículo {ResumeId} no repositório", id);
        var deleted = await repository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Exclusão do currículo {ResumeId} finalizada. Sucesso: {Deleted}", id, deleted);
        return deleted;
    }
}
