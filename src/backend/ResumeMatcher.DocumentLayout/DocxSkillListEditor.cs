using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ResumeMatcher.DocumentLayout;

internal static class DocxSkillListEditor
{
    internal static string ValidateSkill(string skill)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skill);
        if (skill.Any(char.IsControl)) throw new ArgumentException("Provide one skill.", nameof(skill));
        skill = skill.Trim();
        if (skill.IndexOfAny([',', ';', ':']) >= 0 || skill.EndsWith('.'))
            throw new ArgumentException("Provide one skill.", nameof(skill));
        return skill;
    }

    internal static (Text Target, string Replacement) Prepare(Paragraph paragraph, string skill)
    {
        skill = ValidateSkill(skill);
        if (!DocxDocumentInspector.IsSimple(paragraph)) throw new DocxReviewRequiredException("unsupported_skill_content");
        var colon = paragraph.InnerText.IndexOf(':');
        var last = paragraph.Descendants<Text>().LastOrDefault(t => !string.IsNullOrWhiteSpace(t.Text));
        if (colon <= 0 || last is null) throw new DocxReviewRequiredException("unsupported_skill_list");
        var value = paragraph.InnerText[(colon + 1)..].Trim();
        if (value.Length == 0 || last.Text.Contains(':') || value.Contains(',') && value.Contains(';') ||
            Regex.IsMatch(value, @"(?i)\s+(?:e|and|&)\s+") || value.Contains('|'))
            throw new DocxReviewRequiredException("unsupported_skill_list");
        if (value.TrimEnd('.').Split([',', ';']).Any(s => s.Trim().Equals(skill, StringComparison.OrdinalIgnoreCase)))
            throw new DocxReviewRequiredException("duplicate_skill");
        var trimmed = last.Text.TrimEnd();
        var suffix = last.Text[trimmed.Length..];
        var period = trimmed.EndsWith('.') ? "." : "";
        var separator = value.Contains(';') ? "; " : ", ";
        return (last, (period.Length > 0 ? trimmed[..^1] : trimmed) + separator + skill + period + suffix);
    }
}
