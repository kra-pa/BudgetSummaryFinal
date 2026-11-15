using System.Globalization;
namespace BudgetSummaryFinal.Domain;
public static class BudgetCsvParser
{
    private const string Header = "Id,Category,PlannedAmount,ActualAmount,IsApproved";

    public static Result<IReadOnlyList<BudgetRecord>> Parse(string? csv)
    {
        if (TryParse(csv, out var records, out var error)) return Result.Ok(records);
        return Result.Fail<IReadOnlyList<BudgetRecord>>("MalformedCsv", error);
    }

    public static bool TryParse(string? csv, out IReadOnlyList<BudgetRecord> records, out string error)
    {
        records = Array.Empty<BudgetRecord>(); error = string.Empty;
        if (csv is null) { error = "The input is missing."; return false; }
        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.None);
        if (lines.Length == 0 || !string.Equals(lines[0].Trim(), Header, StringComparison.Ordinal)) { error = $"The first line must be '{Header}'."; return false; }
        var parsed = new List<BudgetRecord>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var f = lines[i].Split(',', StringSplitOptions.None);
            if (f.Length != 5 || string.IsNullOrWhiteSpace(f[0]) || string.IsNullOrWhiteSpace(f[1])) { error = $"Line {i + 1} must contain all five fields."; return false; }
            if (!decimal.TryParse(f[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var planned) || !decimal.TryParse(f[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var actual) || planned < 0 || actual < 0) { error = $"Line {i + 1} has an invalid non-negative amount."; return false; }
            if (!bool.TryParse(f[4], out var approved)) { error = $"Line {i + 1} has an invalid IsApproved value; use true or false."; return false; }
            parsed.Add(new BudgetRecord(f[0].Trim(), f[1].Trim(), planned, actual, approved));
        }
        records = Array.AsReadOnly(parsed.ToArray()); return true;
    }
}
