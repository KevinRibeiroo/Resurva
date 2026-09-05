using System.ComponentModel.DataAnnotations;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api.Models;

public sealed record CompareRequestModel(
    Guid ResumeId,
    [param: Required, StringLength(AnalysisConstraints.MaxJobDescriptionLength)] string JobDescription);
