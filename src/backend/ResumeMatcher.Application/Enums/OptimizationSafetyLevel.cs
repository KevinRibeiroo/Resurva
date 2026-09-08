using System.Text.Json.Serialization;

namespace ResumeMatcher.Application;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OptimizationSafetyLevel
{
    Safe,
    NeedsConfirmation,
    Forbidden
}
