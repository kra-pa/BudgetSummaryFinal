namespace BudgetSummaryFinal.Domain;

public sealed record VarianceReportEntry(string Id, string Category, decimal PlannedAmount, decimal ActualAmount, decimal AllowedAmount, decimal RiskScore);
public sealed record CategorySpendingTotal(string Category, int RecordCount, decimal TotalPlanned, decimal TotalActual);

public static class BudgetRiskScoring
{
    // Curried: fix tolerance first, then score any record later.
    public static Func<BudgetRecord, decimal> CreateRiskScore(decimal allowedOverPlanPercent) => record =>
        record.PlannedAmount == 0m ? 0m : Math.Max(0m, (record.ActualAmount / record.PlannedAmount - (1m + allowedOverPlanPercent)) * 100m);

    public static readonly Func<BudgetRecord, decimal> StandardRiskScore = CreateRiskScore(0.10m);
    public static readonly Func<BudgetRecord, decimal> StrictRiskScore = CreateRiskScore(0m);
}

public static class BudgetReports
{
    // Report 1: items exceeding their permitted amount, ordered by risk then ID.
    public static IReadOnlyList<VarianceReportEntry> CreateOverBudgetReport(
        IEnumerable<BudgetRecord> records,
        decimal minimumPlannedAmount,
        Func<BudgetRecord, decimal> allowedAmountPolicy,
        Func<BudgetRecord, decimal> riskScore) =>
        records.WhereApprovedAtLeast(minimumPlannedAmount)
            .Select(record => new VarianceReportEntry(record.Id, record.Category, record.PlannedAmount, record.ActualAmount, allowedAmountPolicy(record), riskScore(record)))
            .Where(entry => entry.ActualAmount > entry.AllowedAmount)
            .OrderByDescending(entry => entry.RiskScore)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToStableSnapshot();

    // Report 2: spending totals by category, ordered deterministically by total then category.
    public static IReadOnlyList<CategorySpendingTotal> CreateCategorySpendingReport(IEnumerable<BudgetRecord> records) =>
        records.GroupBy(record => record.Category, StringComparer.Ordinal)
            .Select(group => new CategorySpendingTotal(group.Key, group.Count(), group.Sum(record => record.PlannedAmount), group.Sum(record => record.ActualAmount)))
            .OrderByDescending(total => total.TotalActual)
            .ThenBy(total => total.Category, StringComparer.Ordinal)
            .ToStableSnapshot();
}

public static class BudgetLazyWorkflow
{
    // Its body does not execute until the caller enumerates the returned IEnumerable.
    public static IEnumerable<BudgetRecord> StreamEligibleRecords(IEnumerable<BudgetRecord> records, decimal minimumPlannedAmount)
    {
        foreach (var record in records)
            if (record.IsApproved && record.PlannedAmount >= minimumPlannedAmount)
                yield return record;
    }

    public static IReadOnlyList<BudgetRecord> MaterializeEligibleSnapshot(IEnumerable<BudgetRecord> records, decimal minimumPlannedAmount) =>
        StreamEligibleRecords(records, minimumPlannedAmount).ToStableSnapshot();
}
