using System.Globalization;
using BudgetSummaryFinal;
using BudgetSummaryFinal.Domain;

var inputPath = GetInputPath(args) ?? Path.Combine(AppContext.BaseDirectory, "data", "budget-records.csv");
var policyName = GetOption(args, "--policy") ?? "standard";
var focusRecordId = GetOption(args, "--record") ?? "B002";
var minimumText = GetOption(args, "--minimum") ?? "100";
var exportPath = GetOption(args, "--export");

var result = decimal.TryParse(minimumText, NumberStyles.Number, CultureInfo.InvariantCulture, out var minimum)
    ? BudgetApplication.Run(inputPath, policyName, minimum, focusRecordId)
    : Result.Fail<BudgetApplicationReport>("InvalidMinimum", "--minimum must be a decimal number.");

if (exportPath is not null)
    result = result.Bind(report => BudgetDataBoundary.ExportCategoryReport(exportPath, report.CategorySpendingReport).Map(_ => report));

switch (result)
{
    case Success<BudgetApplicationReport> success:
        BudgetConsolePresenter.Print(success.Value.Summary, policyName);
        Console.WriteLine($"Focused record: {success.Value.FocusRecord.Id} ({success.Value.FocusRecord.Category}), approved: {success.Value.FocusRecord.IsApproved}");
        BudgetConsolePresenter.PrintReports(success.Value);
        if (exportPath is not null) Console.WriteLine($"Category report exported: {exportPath}");
        break;
    case Failure<BudgetApplicationReport> failure:
        Console.Error.WriteLine($"{failure.Error.Code}: {failure.Error.Message}");
        break;
    default:
        throw new InvalidOperationException("Unknown application result.");
}

static string? GetOption(IReadOnlyList<string> arguments, string option)
{
    for (var index = 0; index + 1 < arguments.Count; index++)
        if (arguments[index] == option) return arguments[index + 1];
    return null;
}

static string? GetInputPath(IReadOnlyList<string> arguments)
{
    for (var index = 0; index < arguments.Count; index++)
    {
        if (arguments[index] is "--policy" or "--minimum" or "--record" or "--export") { index++; continue; }
        if (!arguments[index].StartsWith("--", StringComparison.Ordinal)) return arguments[index];
    }
    return null;
}
