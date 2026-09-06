namespace ResumeMatcher.Api;

public sealed class FirebaseAuthOptions
{
    public const string SectionName = "Authentication:Firebase";

    public string ProjectId { get; set; } = "";
    public string AllowedEmail { get; set; } = "";
}
