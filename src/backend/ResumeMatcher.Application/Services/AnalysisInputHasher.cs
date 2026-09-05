using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResumeMatcher.Application;

public static partial class AnalysisInputHasher
{
    public const string CurrentAnalysisRulesVersion = "v1";

    public static string Generate(
        string resumeText,
        string jobDescription,
        string llmModel,
        string llmConfigurationFingerprint,
        string promptVersion,
        string analysisRulesVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(llmModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(llmConfigurationFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRulesVersion);

        var canonicalInput = JsonSerializer.Serialize(new[]
        {
            NormalizeForHash(resumeText),
            NormalizeForHash(jobDescription),
            llmModel.Trim(),
            llmConfigurationFingerprint.Trim(),
            promptVersion.Trim(),
            analysisRulesVersion.Trim()
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalInput)));
    }

    public static string NormalizeForHash(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return WhitespaceRegex().Replace(input.Trim(), " ");
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
