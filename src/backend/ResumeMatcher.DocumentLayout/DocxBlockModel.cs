namespace ResumeMatcher.DocumentLayout;

public sealed record DocxBlockModel(string Id, int Index, string Text,
    string? StyleId, string? Section, string Kind, bool Editable);
