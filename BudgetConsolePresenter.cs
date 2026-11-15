using BudgetSummaryFinal.Domain;
namespace BudgetSummaryFinal;
public static class BudgetConsolePresenter
{
    public static void Print(BudgetSummary summary, string policy)
    {
        Console.WriteLine($"\n--- {policy} ---");
        foreach (var x in summary.Assessments) Console.WriteLine($"{x.Record.Id,-4} {x.Record.Category,-12} planned: {x.Record.PlannedAmount,7:C} actual: {x.Record.ActualAmount,7:C} status: {(x.IsOverBudget ? "OVER" : "within limit")}");
        Console.WriteLine($"Included: {summary.Assessments.Count}; planned: {summary.TotalPlanned:C}; actual: {summary.TotalActual:C}; variance: {summary.TotalVariance:C}; over budget: {summary.OverBudgetCount}");
    }

    public static void PrintReports(BudgetApplicationReport report)
    {
        Console.WriteLine($"Native utilization: {report.NativeUtilizationPercent}%");
        Console.WriteLine("\n--- Over-budget report ---");
        foreach (var entry in report.OverBudgetReport)
            Console.WriteLine($"{entry.Id,-4} {entry.Category,-12} actual: {entry.ActualAmount,7:C} allowed: {entry.AllowedAmount,7:C} risk: {entry.RiskScore:F1}");
        Console.WriteLine("\n--- Category spending report ---");
        foreach (var total in report.CategorySpendingReport)
            Console.WriteLine($"{total.Category,-12} records: {total.RecordCount} planned: {total.TotalPlanned,7:C} actual: {total.TotalActual,7:C}");
    }
}
