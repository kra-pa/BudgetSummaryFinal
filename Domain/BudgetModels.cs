namespace BudgetSummaryFinal.Domain;
public readonly record struct BudgetRecord(string Id, string Category, decimal PlannedAmount, decimal ActualAmount, bool IsApproved);
public readonly record struct BudgetAssessment(BudgetRecord Record, decimal AllowedAmount, decimal Variance, bool IsOverBudget);
public sealed record BudgetSummary
{
    public BudgetSummary(IEnumerable<BudgetAssessment> assessments) => Assessments = Array.AsReadOnly(assessments.OrderBy(x => x.Record.Category, StringComparer.Ordinal).ThenBy(x => x.Record.Id, StringComparer.Ordinal).ToArray());
    public IReadOnlyList<BudgetAssessment> Assessments { get; }
    public decimal TotalPlanned => Assessments.Sum(x => x.Record.PlannedAmount);
    public decimal TotalActual => Assessments.Sum(x => x.Record.ActualAmount);
    public decimal TotalVariance => TotalActual - TotalPlanned;
    public int OverBudgetCount => Assessments.Count(x => x.IsOverBudget);
}
