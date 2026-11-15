namespace BudgetSummaryFinal.Domain;
public static class BudgetPipeline
{
    // Stage 1: keep approved records whose planned amount meets the summary threshold.
    public static IReadOnlyList<BudgetRecord> SelectEligibleRecords(IEnumerable<BudgetRecord> records, decimal minimum) => records.WhereApprovedAtLeast(minimum).ToStableSnapshot();
    // Stage 2: calculate variance and invoke the supplied spending-limit policy.
    public static IReadOnlyList<BudgetAssessment> AssessSpending(IEnumerable<BudgetRecord> records, Func<BudgetRecord, decimal> policy) => 
        Array.AsReadOnly(records.Select(record => { var allowed = policy(record); 
        return new BudgetAssessment(record, allowed, record.ActualAmount - record.PlannedAmount, record.ActualAmount > allowed); })
        .ToArray());
    // Stage 3: materialise a stable, ordered summary for presentation or export.
    public static BudgetSummary CreateSummary(IEnumerable<BudgetAssessment> assessments) => new(assessments);
    public static BudgetSummary ProduceSummary(IEnumerable<BudgetRecord> records, Func<BudgetRecord, decimal> policy, decimal minimum) => CreateSummary(AssessSpending(SelectEligibleRecords(records, minimum), policy));
}
public static class BudgetPolicies
{
    public static readonly Func<BudgetRecord, decimal> Standard = x => x.PlannedAmount * 1.10m;
    public static readonly Func<BudgetRecord, decimal> Strict = x => x.PlannedAmount;
    public static IReadOnlyDictionary<string, Func<BudgetRecord, decimal>> All { get; } = new Dictionary<string, Func<BudgetRecord, decimal>> { ["Standard (10% tolerance)"] = Standard, ["Strict (no tolerance)"] = Strict };
    public static bool TryGet(string? name, out Func<BudgetRecord, decimal>? policy)
    {
        policy = name?.Trim().ToLowerInvariant() switch { "standard" => Standard, "strict" => Strict, _ => null };
        return policy is not null;
    }
}
