namespace BudgetSummaryFinal.Domain;

public static class BudgetExtensions
{
    // This belongs on a sequence of records because it is a reusable domain selection.
    public static IEnumerable<BudgetRecord> WhereApprovedAtLeast(this IEnumerable<BudgetRecord> records, decimal minimumPlannedAmount) =>
        records.Where(record => record.IsApproved && record.PlannedAmount >= minimumPlannedAmount);

    // This belongs on IEnumerable because it deliberately creates an immutable reusable snapshot.
    public static IReadOnlyList<T> ToStableSnapshot<T>(this IEnumerable<T> source) => Array.AsReadOnly(source.ToArray());
}
