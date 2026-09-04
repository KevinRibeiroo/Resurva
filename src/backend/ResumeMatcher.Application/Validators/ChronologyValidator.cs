using System.Text.RegularExpressions;

namespace ResumeMatcher.Application;

public static partial class ChronologyValidator
{
    private static readonly string[] PresentKeywords =
    [
        "atual", "presente", "present", "current", "hoje", "momento", "o momento"
    ];

    public static ChronologyValidationModel ValidatePeriod(string? periodText, DateOnly referenceDate)
    {
        if (string.IsNullOrWhiteSpace(periodText))
        {
            return new ChronologyValidationModel(
                IsValid: true,
                IsFuture: false,
                IsIndeterminate: true,
                Message: "Período não informado.");
        }

        var parts = Regex.Split(periodText, @"\s*(?:[-–—]|\b(?:até|a|to)\b)\s*", RegexOptions.IgnoreCase);
        if (parts.Length < 2)
        {
            // Only a single date or unparseable phrase
            var singleDate = TryParseYearMonth(parts[0].Trim());
            if (singleDate is null)
            {
                return new ChronologyValidationModel(
                    IsValid: true,
                    IsFuture: false,
                    IsIndeterminate: true,
                    Message: "Formato de data não reconhecido como intervalo.");
            }

            var isFutureSingle = IsAfterReference(singleDate.Value.Year, singleDate.Value.Month, referenceDate);
            return new ChronologyValidationModel(
                IsValid: !isFutureSingle,
                IsFuture: isFutureSingle,
                IsIndeterminate: false,
                Message: isFutureSingle ? "Data informada é posterior à data de referência." : "Data válida.");
        }

        var startPart = parts[0].Trim();
        var endPart = parts[1].Trim();

        var parsedStart = TryParseYearMonth(startPart);
        if (parsedStart is null)
        {
            return new ChronologyValidationModel(
                IsValid: true,
                IsFuture: false,
                IsIndeterminate: true,
                Message: "Data de início incompleta ou não reconhecida.");
        }

        var isStartFuture = IsAfterReference(parsedStart.Value.Year, parsedStart.Value.Month, referenceDate);
        if (isStartFuture)
        {
            return new ChronologyValidationModel(
                IsValid: false,
                IsFuture: true,
                IsIndeterminate: false,
                Message: $"Data de início ({startPart}) é posterior à data de referência ({referenceDate:yyyy-MM-dd}).");
        }

        var isPresent = PresentKeywords.Any(k => endPart.Equals(k, StringComparison.OrdinalIgnoreCase));
        if (isPresent)
        {
            return new ChronologyValidationModel(
                IsValid: true,
                IsFuture: false,
                IsIndeterminate: false,
                Message: "Experiência em andamento até o momento atual.");
        }

        var parsedEnd = TryParseYearMonth(endPart);
        if (parsedEnd is null)
        {
            return new ChronologyValidationModel(
                IsValid: true,
                IsFuture: false,
                IsIndeterminate: true,
                Message: "Data de término não reconhecida com precisão.");
        }

        var isEndFuture = IsAfterReference(parsedEnd.Value.Year, parsedEnd.Value.Month, referenceDate);
        if (isEndFuture)
        {
            return new ChronologyValidationModel(
                IsValid: false,
                IsFuture: true,
                IsIndeterminate: false,
                Message: $"Data de término ({endPart}) é posterior à data de referência ({referenceDate:yyyy-MM-dd}).");
        }

        if (parsedStart.Value.Year > parsedEnd.Value.Year ||
            (parsedStart.Value.Year == parsedEnd.Value.Year && parsedStart.Value.Month > parsedEnd.Value.Month))
        {
            return new ChronologyValidationModel(
                IsValid: false,
                IsFuture: false,
                IsIndeterminate: false,
                Message: "Data de início é posterior à data de término.");
        }

        return new ChronologyValidationModel(
            IsValid: true,
            IsFuture: false,
            IsIndeterminate: false,
            Message: "Período cronológico consistente.");
    }

    public static bool IsAfterReference(int year, int month, DateOnly referenceDate)
    {
        if (year > referenceDate.Year)
            return true;

        if (year == referenceDate.Year && month > referenceDate.Month)
            return true;

        return false;
    }

    private static (int Year, int Month)? TryParseYearMonth(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var clean = text.Trim();

        // Check MM/yyyy or M/yyyy
        var mmYyyyMatch = MmYyyyRegex().Match(clean);
        if (mmYyyyMatch.Success)
        {
            var month = int.Parse(mmYyyyMatch.Groups[1].Value);
            var year = int.Parse(mmYyyyMatch.Groups[2].Value);
            if (month is >= 1 and <= 12)
                return (year, month);
        }

        // Check MonthName/yyyy (e.g., Mai/2026, May 2026)
        var textMonthMatch = TextMonthYearRegex().Match(clean);
        if (textMonthMatch.Success)
        {
            var monthName = textMonthMatch.Groups[1].Value.ToLowerInvariant();
            var year = int.Parse(textMonthMatch.Groups[2].Value);
            var month = ParseMonthName(monthName);
            if (month.HasValue)
                return (year, month.Value);
        }

        // Check Year only (e.g., 2026)
        var yyyyMatch = YyyyRegex().Match(clean);
        if (yyyyMatch.Success)
        {
            var year = int.Parse(yyyyMatch.Groups[1].Value);
            return (year, 1);
        }

        return null;
    }

    private static int? ParseMonthName(string name)
    {
        return name switch
        {
            var m when m.StartsWith("jan") => 1,
            var m when m.StartsWith("fev") || m.StartsWith("feb") => 2,
            var m when m.StartsWith("mar") => 3,
            var m when m.StartsWith("abr") || m.StartsWith("apr") => 4,
            var m when m.StartsWith("mai") || m.StartsWith("may") => 5,
            var m when m.StartsWith("jun") => 6,
            var m when m.StartsWith("jul") => 7,
            var m when m.StartsWith("ago") || m.StartsWith("aug") => 8,
            var m when m.StartsWith("set") || m.StartsWith("sep") => 9,
            var m when m.StartsWith("out") || m.StartsWith("oct") => 10,
            var m when m.StartsWith("nov") => 11,
            var m when m.StartsWith("dez") || m.StartsWith("dec") => 12,
            _ => null
        };
    }

    [GeneratedRegex(@"^(?:0?([1-9]|1[0-2]))[/-](\d{4})$")]
    private static partial Regex MmYyyyRegex();

    [GeneratedRegex(@"^([a-zA-ZçÇ]{3,})[/\s]+(\d{4})$")]
    private static partial Regex TextMonthYearRegex();

    [GeneratedRegex(@"^\b(\d{4})\b$")]
    private static partial Regex YyyyRegex();
}
